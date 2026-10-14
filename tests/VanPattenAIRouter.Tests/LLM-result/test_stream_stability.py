#!/usr/bin/env python3
import asyncio
import aiohttp
import json
import time
import uuid
from datetime import datetime, timezone
from typing import Optional, Dict, Any, List


COMPLEX_PROMPT = """
You are an expert distributed systems engineer. Create a comprehensive Go implementation of a distributed consensus algorithm inspired by Raft and Paxos, with production-grade considerations.

Requirements:

1. **Core Algorithm Design**
   - Design a Raft/Paxos hybrid or a well-documented Raft implementation
   - Include state machine, log replication, leader election, and safety guarantees
   - Explain the trade-offs made and why

2. **Go Implementation**
   - Provide complete, idiomatic Go code for all core components:
     - Node/Server structure with roles (Leader, Follower, Candidate)
     - RPC communication (AppendEntries, RequestVote, etc.)
     - Persistent log and state storage
     - Volatile state management
     - Timer management (election timeout, heartbeat)
   - Include proper error handling, context usage, and concurrency safety
   - Add godoc comments for all public types and methods

3. **Key-Value Store Integration**
   - Build a replicated KV store on top of the consensus layer
   - Support GET, PUT, DELETE operations with linearizability
   - Handle client sessions and request deduplication
   - Include read consistency options

4. **Performance Analysis**
   - Analyze theoretical performance: throughput, latency, tail latency
   - Discuss bottlenecks (disk I/O, network, serialization)
   - Suggest optimizations: batching, pipelining, log compaction/snapshotting
   - Provide back-of-envelope calculations for typical cluster sizes (3, 5, 7 nodes)

5. **Testing Strategy**
   - Unit tests: log replication, election, safety properties
   - Integration tests: cluster behavior under partitions, leader failures, node recovery
   - Property-based testing ideas (Jepsen-style scenarios)
   - Provide example test cases in Go

6. **Operational Considerations**
   - Deployment topology and configuration
   - Monitoring, metrics, and observability (Prometheus format ideas)
   - Security considerations (TLS, authentication, authorization)
   - Disaster recovery and backup strategies

Please provide the full implementation with complete code blocks, detailed explanations for each section, and a final summary. Make it thorough and production-ready.
"""


class StreamMetrics:
    def __init__(self, scenario_name: str):
        self.scenario_name = scenario_name
        self.total_chunks = 0
        self.content_length = 0
        self.duration = 0.0
        self.time_to_first_chunk = 0.0
        self.slow_chunks = 0  # gaps > 5s
        self.disconnects = 0
        self.empty_streams = False
        self.errors: List[str] = []
        self.last_chunk_time: Optional[float] = None
        self.first_chunk_received = False
        self.start_time: Optional[float] = None
        self.end_time: Optional[float] = None
        self.success = False

    def to_dict(self) -> Dict[str, Any]:
        return {
            "scenario_name": self.scenario_name,
            "total_chunks": self.total_chunks,
            "content_length": self.content_length,
            "duration": self.duration,
            "time_to_first_chunk": self.time_to_first_chunk,
            "slow_chunks": self.slow_chunks,
            "disconnects": self.disconnects,
            "empty_streams": self.empty_streams,
            "errors": self.errors,
            "success": self.success,
        }


async def make_stream_request(
    session: aiohttp.ClientSession,
    model: str,
    prompt: str,
    max_tokens: int = 16384,
    timeout: int = 300,
) -> StreamMetrics:
    metrics = StreamMetrics(scenario_name=model)
    metrics.start_time = time.time()
    last_chunk_ts = metrics.start_time

    try:
        payload = {
            "model": model,
            "messages": [{"role": "user", "content": prompt}],
            "max_tokens": max_tokens,
            "stream": True,
            "temperature": 0.7,
        }

        headers = {
            "Content-Type": "application/json",
            "Accept": "text/event-stream",
        }

        async with session.post(
            "http://localhost:10080/v1/chat/completions",
            json=payload,
            headers=headers,
            timeout=aiohttp.ClientTimeout(total=timeout),
        ) as response:
            if response.status != 200:
                error_text = await response.text()
                metrics.errors.append(f"HTTP {response.status}: {error_text}")
                metrics.end_time = time.time()
                metrics.duration = metrics.end_time - (metrics.start_time or metrics.end_time)
                return metrics

            # Stream the response
            chunk_count = 0
            async for line in response.content:
                now = time.time()
                line_str = line.decode("utf-8", errors="replace").strip()

                if not line_str:
                    continue

                # Handle SSE format
                if line_str.startswith("data: "):
                    data_str = line_str[6:]
                    if data_str == "[DONE]":
                        break
                    try:
                        data = json.loads(data_str)
                        # Extract content from delta
                        choices = data.get("choices", [])
                        if choices:
                            delta = choices[0].get("delta", {})
                            content = delta.get("content") or ""
                            if content:
                                metrics.content_length += len(content)
                                if not metrics.first_chunk_received:
                                    metrics.first_chunk_received = True
                                    metrics.time_to_first_chunk = now - (metrics.start_time or now)
                                # Check for slow chunks
                                if metrics.last_chunk_time:
                                    gap = now - metrics.last_chunk_time
                                    if gap > 5.0:
                                        metrics.slow_chunks += 1
                                metrics.last_chunk_time = now
                    except json.JSONDecodeError:
                        # Skip malformed chunks but count
                        pass

                chunk_count += 1
                metrics.total_chunks = chunk_count

                # Report progress every 50 chunks
                if chunk_count % 50 == 0:
                    elapsed = now - (metrics.start_time or now)
                    print(f"  [{model}] Progress: {chunk_count} chunks, {metrics.content_length} chars, {elapsed:.2f}s")

            metrics.end_time = time.time()
            metrics.duration = metrics.end_time - (metrics.start_time or metrics.end_time)
            metrics.success = metrics.total_chunks > 0 and metrics.content_length > 0
            metrics.empty_streams = metrics.total_chunks == 0

            if not metrics.success and not metrics.errors:
                metrics.errors.append("Stream completed with no content")

            return metrics

    except asyncio.TimeoutError:
        metrics.errors.append("Request timeout")
        metrics.end_time = time.time()
        if metrics.start_time:
            metrics.duration = metrics.end_time - metrics.start_time
        metrics.disconnects += 1
        return metrics
    except aiohttp.ClientConnectorError as e:
        metrics.errors.append(f"Connection error: {str(e)}")
        metrics.end_time = time.time()
        if metrics.start_time:
            metrics.duration = metrics.end_time - metrics.start_time
        metrics.disconnects += 1
        return metrics
    except Exception as e:
        metrics.errors.append(f"Unexpected error: {str(e)}")
        metrics.end_time = time.time()
        if metrics.start_time:
            metrics.duration = metrics.end_time - metrics.start_time
        return metrics


async def run_with_retry(
    session: aiohttp.ClientSession,
    model: str,
    prompt: str,
    max_retries: int = 2,
    max_tokens: int = 16384,
) -> StreamMetrics:
    last_error = None
    for attempt in range(max_retries + 1):
        if attempt > 0:
            backoff = 2 ** attempt  # exponential backoff
            print(f"  [{model}] Retrying... attempt {attempt + 1}/{max_retries + 1} after {backoff}s")
            await asyncio.sleep(backoff)

        metrics = await make_stream_request(session, model, prompt, max_tokens=max_tokens)
        if metrics.success:
            return metrics
        last_error = metrics.errors[-1] if metrics.errors else "Unknown error"

    # If all retries failed, return last metrics
    print(f"  [{model}] Failed after {max_retries + 1} attempts: {last_error}")
    return metrics


async def warm_up(session: aiohttp.ClientSession):
    print("\n=== WARM-UP ===")
    warm_prompt = "Write a brief Hello World in Go."
    metrics = await run_with_retry(session, "vanpatten/auto", warm_prompt, max_retries=1, max_tokens=100)
    if metrics.success:
        print(f"Warm-up successful: {metrics.content_length} chars in {metrics.duration:.2f}s")
    else:
        print(f"Warm-up failed: {metrics.errors}")
    print("=" * 60)


async def run_scenario(session: aiohttp.ClientSession, model: str, scenario_num: int) -> StreamMetrics:
    print(f"\n=== SCENARIO {scenario_num}/3: {model} ===")
    metrics = await run_with_retry(session, model, COMPLEX_PROMPT, max_retries=2, max_tokens=16384)
    print(f"Completed: {metrics.total_chunks} chunks, {metrics.content_length} chars, {metrics.duration:.2f}s")
    print(f"  TTFC: {metrics.time_to_first_chunk:.3f}s, Slow chunks: {metrics.slow_chunks}, Disconnects: {metrics.disconnects}")
    if metrics.errors:
        print(f"  Errors: {metrics.errors}")
    print("=" * 60)
    return metrics


def save_results(results: List[StreamMetrics], output_dir: Optional[str] = None):
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    filename = f"stream_stability_results_{timestamp}.json"

    if output_dir:
        import os
        os.makedirs(output_dir, exist_ok=True)
        filename = os.path.join(output_dir, filename)

    data = {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "scenarios": [r.to_dict() for r in results],
    }

    with open(filename, "w") as f:
        json.dump(data, f, indent=2)

    print(f"\n✓ Results saved to: {filename}")
    return filename


def print_summary(results: List[StreamMetrics]):
    print("\n" + "=" * 80)
    print("STREAM STABILITY TEST SUMMARY")
    print("=" * 80)

    for i, r in enumerate(results, 1):
        status = "✓ SUCCESS" if r.success else "✗ FAILED"
        print(f"\n{i}. {r.scenario_name} [{status}]")
        print(f"   Chunks:           {r.total_chunks}")
        print(f"   Content Length:   {r.content_length:,} chars")
        print(f"   Duration:         {r.duration:.3f}s")
        print(f"   Throughput:       {r.content_length / r.duration:.1f} chars/s" if r.duration > 0 else "   Throughput:       N/A")
        print(f"   TTFC:             {r.time_to_first_chunk:.3f}s")
        print(f"   Slow Chunks (>5s): {r.slow_chunks}")
        print(f"   Disconnects:      {r.disconnects}")
        print(f"   Empty Stream:     {r.empty_streams}")
        if r.errors:
            print(f"   Errors:           {r.errors}")

    # Comparison
    if len(results) == 3 and all(r.duration > 0 for r in results if r.success):
        print("\n" + "-" * 80)
        print("COMPARISON")
        print("-" * 80)
        successful = [r for r in results if r.success]
        if successful:
            avg_throughput = sum(r.content_length / r.duration for r in successful) / len(successful)
            print(f"   Avg Throughput:   {avg_throughput:.1f} chars/s")
            print(f"   Best Throughput:  {max(r.content_length / r.duration for r in successful):.1f} chars/s ({successful[[r.content_length/r.duration for r in successful].index(max(r.content_length/r.duration for r in successful))].scenario_name})")
        print(f"   Consistency Check: Scenario 1 ({results[0].scenario_name}) vs Scenario 3 ({results[2].scenario_name})")
        if results[0].success and results[2].success:
            diff = abs((results[0].content_length / results[0].duration) - (results[2].content_length / results[2].duration))
            print(f"     Throughput diff: {diff:.1f} chars/s")
    print("=" * 80)


async def main():
    print("VanPatten AI Router - Stream Stability Stress Test")
    print(f"Target: http://localhost:10080")
    print(f"Time: {datetime.now(timezone.utc).isoformat()}")
    print("-" * 60)

    async with aiohttp.ClientSession() as session:
        # Warm-up
        await warm_up(session)

        # Run 3 scenarios in sequence
        results = []
        results.append(await run_scenario(session, "vanpatten/auto", 1))
        results.append(await run_scenario(session, "vanpatten/auto-free", 2))
        results.append(await run_scenario(session, "vanpatten/auto", 3))

        # Save and summarize
        save_results(results, output_dir="tests/VanPattenAIRouter.Tests/LLM-result")
        print_summary(results)


if __name__ == "__main__":
    asyncio.run(main())
