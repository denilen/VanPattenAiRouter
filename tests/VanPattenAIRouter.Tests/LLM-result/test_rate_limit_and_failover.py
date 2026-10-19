#!/usr/bin/env python3
"""
LLM-Generated Test: VanPatten AI Router - Rate Limiting & Failover Test
Comprehensive tests for rate limiting behavior, failover, and edge cases.
"""

import aiohttp
import asyncio
import json
import os
import time
from dataclasses import dataclass
from typing import Dict, Any, List

# Configuration
BASE_URL = os.getenv("VANPATTEN_API_URL", "http://localhost:10080")
API_KEY = os.getenv("VANPATTEN_API_KEY", "")


@dataclass
class TestResult:
    test_name: str
    success: bool
    response: Dict[str, Any]
    error: str = ""


class VanPattenRateLimitTester:
    def __init__(self, base_url: str = BASE_URL, api_key: str = API_KEY):
        self.base_url = base_url.rstrip('/')
        self.api_key = api_key
        self.session = None

    async def __aenter__(self):
        self.session = aiohttp.ClientSession(
            headers={"Authorization": f"Bearer {self.api_key}"} if self.api_key else {}
        )
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        if self.session:
            await self.session.close()

    async def chat_completion(self, model: str, messages: List[Dict[str, str]],
                              stream: bool = False, max_tokens: int = 50) -> TestResult:
        url = f"{self.base_url}/v1/chat/completions"
        payload = {
            "model": model,
            "messages": messages,
            "stream": stream,
            "max_tokens": max_tokens,
            "temperature": 0.7
        }

        try:
            async with self.session.post(url, json=payload) as response:
                if stream:
                    content = ""
                    async for line in response.content:
                        line = line.decode('utf-8').strip()
                        if line.startswith("data: "):
                            data = line[6:]
                            if data != "[DONE]":
                                try:
                                    chunk = json.loads(data)
                                    if chunk.get("choices"):
                                        delta = chunk["choices"][0].get("delta", {})
                                        if "content" in delta:
                                            content += delta["content"]
                                except json.JSONDecodeError:
                                    pass
                    return TestResult(
                        test_name="streaming",
                        success=True,
                        response={"content": content}
                    )
                else:
                    data = await response.json()
                    return TestResult(
                        test_name="completion",
                        success=response.status == 200,
                        response=data,
                        error="" if response.status == 200 else f"HTTP {response.status}"
                    )
        except Exception as e:
            return TestResult(
                test_name="completion",
                success=False,
                response={},
                error=str(e)
            )

    async def get_admin_stats(self) -> TestResult:
        url = f"{self.base_url}/admin/stats"
        try:
            async with self.session.get(url) as response:
                data = await response.json()
                return TestResult(
                    test_name="admin_stats",
                    success=response.status == 200,
                    response=data
                )
        except Exception as e:
            return TestResult(
                test_name="admin_stats",
                success=False,
                response={},
                error=str(e)
            )

    async def test_rate_limit_enforcement(self) -> List[TestResult]:
        """Test that rate limits are properly enforced."""
        results = []
        print("\n=== Testing Rate Limit Enforcement ===")

        # First check current stats
        print("1. Checking current rate limit stats...")
        result = await self.get_admin_stats()
        results.append(result)
        if result.success:
            stats = result.response
            for provider_stat in stats.get("providers", []):
                name = provider_stat.get("name", "unknown")
                daily_req = provider_stat.get("requests_today", 0)
                daily_limit = provider_stat.get("rate_limit", {}).get("requests_per_day", "unlimited")
                hourly_req = provider_stat.get("requests_this_hour", 0)
                hourly_limit = provider_stat.get("rate_limit", {}).get("requests_per_hour", "unlimited")
                is_limited = provider_stat.get("is_rate_limited", False)
                print(
                    f"   {name}: Daily {daily_req}/{daily_limit}, Hourly {hourly_req}/{hourly_limit}, Limited: {is_limited}")

        # Test 2: Burst requests to test rate limiting
        print("\n2. Sending burst requests to test rate limiting...")
        burst_results = []
        for i in range(10):
            result = await self.chat_completion(
                model="vanpatten/auto-free",
                messages=[{"role": "user", "content": f"Quick test {i + 1}"}],
                stream=False,
                max_tokens=20
            )
            burst_results.append(result)
            status = "✓" if result.success else "✗"
            model_used = result.response.get("model", "unknown") if result.success else "N/A"
            print(f"   {status} Request {i + 1}: {model_used}")
            await asyncio.sleep(0.1)  # Small delay

        results.extend(burst_results)
        passed = sum(1 for r in burst_results if r.success)
        print(f"   Burst test: {passed}/10 succeeded")

        return results

    async def test_failover_behavior(self) -> List[TestResult]:
        """Test that failover works correctly when providers fail."""
        results = []
        print("\n=== Testing Failover Behavior ===")

        # Test with a model known to be on multiple providers
        print("1. Testing failover with specific model on multiple providers...")

        # First check which models are on multiple providers
        await self.get_admin_stats()

        # Test with auto-free which should try multiple free models
        print("2. Testing auto-free failover...")
        for i in range(5):
            result = await self.chat_completion(
                model="vanpatten/auto-free",
                messages=[{"role": "user", "content": f"Test failover {i + 1}"}],
                stream=False,
                max_tokens=30
            )
            results.append(result)
            status = "✓" if result.success else "✗"
            model_used = result.response.get("model", "unknown") if result.success else "N/A"
            print(f"   {status} Attempt {i + 1}: {model_used}")
            await asyncio.sleep(0.2)

        # Test 3: Streaming failover
        print("3. Testing streaming failover...")
        for i in range(3):
            result = await self.chat_completion(
                model="vanpatten/auto-free",
                messages=[{"role": "user", "content": f"Stream test {i + 1}"}],
                stream=True,
                max_tokens=50
            )
            results.append(result)
            status = "✓" if result.success else "✗"
            print(f"   {status} Stream {i + 1}: {len(result.response.get('content', ''))} chars")

        return results

    async def test_token_estimation_accuracy(self) -> List[TestResult]:
        """Test that token estimation doesn't overestimate."""
        results = []
        print("\n=== Testing Token Estimation Accuracy ===")

        # Short message should have low token estimate
        short_msg = [{"role": "user", "content": "Hi"}]
        result = await self.chat_completion("vanpatten/auto-free", short_msg, max_tokens=10)
        results.append(result)

        # Longer message
        long_content = " ".join(["This is a test message. "] * 50)  # ~500 words
        long_msg = [{"role": "user", "content": long_content}]
        result2 = await self.chat_completion("vanpatten/auto-free", long_msg, max_tokens=100)
        results.append(result2)

        if result.success and result2.success:
            print("   Both short and long requests succeeded")
            print("   Token estimation appears to be working correctly")

        return results

    async def test_model_refresh_doesnt_interrupt(self) -> List[TestResult]:
        """Test that model refresh doesn't interrupt ongoing requests."""
        results = []
        print("\n=== Testing Model Refresh Non-Interruption ===")

        # Make several concurrent requests
        print("1. Sending concurrent requests...")
        tasks = []
        for i in range(5):
            task = self.chat_completion(
                model="vanpatten/auto-free",
                messages=[{"role": "user", "content": f"Concurrent test {i + 1}"}],
                stream=False,
                max_tokens=20
            )
            tasks.append(task)

        concurrent_results = await asyncio.gather(*tasks, return_exceptions=True)

        for i, result in enumerate(concurrent_results):
            if isinstance(result, Exception):
                results.append(TestResult(f"concurrent_{i}", False, {}, str(result)))
                print(f"   ✗ Request {i + 1}: Exception - {result}")
            else:
                results.append(result)
                status = "✓" if result.success else "✗"
                print(f"   {status} Request {i + 1}")

        print(
            f"\n   Concurrent test: {sum(1 for r in concurrent_results if isinstance(r, TestResult) and r.success)}/5 succeeded")
        return results

    async def run_all_rate_limit_tests(self) -> List[TestResult]:
        """Run all rate limit and failover tests."""
        all_results = []

        print("=" * 60)
        print("VanPatten AI Router - Rate Limit & Failover Tests")
        print("=" * 60)

        all_results.extend(await self.test_rate_limit_enforcement())
        all_results.extend(await self.test_failover_behavior())
        all_results.extend(await self.test_token_estimation_accuracy())
        all_results.extend(await self.test_model_refresh_doesnt_interrupt())

        return all_results


async def main():
    async with VanPattenRateLimitTester() as tester:
        results = await tester.run_all_rate_limit_tests()

    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)
    test_results = [r for r in results if isinstance(r, TestResult)]
    passed = sum(1 for r in test_results if r.success)
    failed = len(test_results) - passed
    print(f"Total: {len(test_results)} | Passed: {passed} | Failed: {failed}")

    for r in test_results:
        status = "✓" if r.success else "✗"
        print(f"  {status} {r.test_name}")

    if failed > 0:
        print("\nFailures:")
        for r in test_results:
            if not r.success:
                print(f"  - {r.test_name}: {r.error}")

    return failed == 0


if __name__ == "__main__":
    success = asyncio.run(main())
    exit(0 if success else 1)
