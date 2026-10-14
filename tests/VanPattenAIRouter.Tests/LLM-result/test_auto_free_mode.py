#!/usr/bin/env python3
"""
LLM-Generated Test: VanPatten AI Router - Auto-Free Mode Test
Tests the vanpatten/auto-free virtual model which routes to free models only.
"""

import asyncio
import aiohttp
import json
import os
from typing import Dict, Any, List
from dataclasses import dataclass

# Configuration
BASE_URL = os.getenv("VANPATTEN_API_URL", "http://localhost:10080")
API_KEY = os.getenv("VANPATTEN_API_KEY", "")

@dataclass
class TestResult:
    test_name: str
    success: bool
    response: Dict[str, Any]
    error: str = ""

class VanPattenAutoFreeTester:
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

    async def chat_completion(self, model: str, messages: List[Dict[str, str]], stream: bool = False) -> TestResult:
        """Send a chat completion request."""
        url = f"{self.base_url}/v1/chat/completions"
        payload = {
            "model": model,
            "messages": messages,
            "stream": stream,
            "max_tokens": 100,
            "temperature": 0.7
        }

        try:
            async with self.session.post(url, json=payload) as response:
                if stream:
                    # For streaming, read chunks
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
                        test_name="streaming_completion",
                        success=True,
                        response={"content": content}
                    )
                else:
                    data = await response.json()
                    return TestResult(
                        test_name="completion",
                        success=response.status == 200,
                        response=data
                    )
        except Exception as e:
            return TestResult(
                test_name="completion",
                success=False,
                response={},
                error=str(e)
            )

    async def list_models(self) -> TestResult:
        """List available models."""
        url = f"{self.base_url}/v1/models"
        try:
            async with self.session.get(url) as response:
                data = await response.json()
                return TestResult(
                    test_name="list_models",
                    success=response.status == 200,
                    response=data
                )
        except Exception as e:
            return TestResult(
                test_name="list_models",
                success=False,
                response={},
                error=str(e)
            )

    async def run_auto_free_tests(self) -> List[TestResult]:
        """Run comprehensive tests for vanpatten/auto-free mode."""
        results = []

        print("=== Testing vanpatten/auto-free Mode ===\n")

        # Test 1: List models to see what's available
        print("1. Listing available models...")
        result = await self.list_models()
        results.append(result)
        if result.success:
            models = result.response.get("data", [])
            free_models = [m for m in models if m.get("is_free")]
            print(f"   Found {len(models)} total models, {len(free_models)} free models")
            for m in free_models[:5]:
                print(f"   - {m['id']} (provider: {m.get('provider', 'unknown')})")
        else:
            print(f"   ERROR: {result.error}")

        # Test 2: Simple non-streaming completion
        print("\n2. Testing non-streaming completion with auto-free...")
        result = await self.chat_completion(
            model="vanpatten/auto-free",
            messages=[{"role": "user", "content": "Write a simple Python function to calculate fibonacci numbers"}],
            stream=False
        )
        results.append(result)
        if result.success:
            content = result.response.get("choices", [{}])[0].get("message", {}).get("content", "")
            print(f"   SUCCESS: Generated {len(content)} characters")
            print(f"   Model used: {result.response.get('model', 'unknown')}")
        else:
            print(f"   ERROR: {result.error}")

        # Test 3: Streaming completion
        print("\n3. Testing streaming completion with auto-free...")
        result = await self.chat_completion(
            model="vanpatten/auto-free",
            messages=[{"role": "user", "content": "Write a Python hello world"}],
            stream=True
        )
        results.append(result)
        if result.success:
            print(f"   SUCCESS: Streamed {len(result.response.get('content', ''))} characters")
        else:
            print(f"   ERROR: {result.error}")

        # Test 4: Multiple requests to test failover
        print("\n4. Testing multiple requests (tests failover)...")
        for i in range(3):
            result = await self.chat_completion(
                model="vanpatten/auto-free",
                messages=[{"role": "user", "content": f"Count to {i+1} in Python"}],
                stream=False
            )
            results.append(result)
            status = "SUCCESS" if result.success else f"FAILED: {result.error}"
            print(f"   Request {i+1}: {status}")

        # Test 5: Edge case - empty message
        print("\n5. Testing edge case - minimal request...")
        result = await self.chat_completion(
            model="vanpatten/auto-free",
            messages=[{"role": "user", "content": "hi"}],
            stream=False
        )
        results.append(result)
        if result.success:
            print("   SUCCESS: Handled minimal request")
        else:
            print(f"   ERROR: {result.error}")

        return results


async def main():
    async with VanPattenAutoFreeTester() as tester:
        results = await tester.run_auto_free_tests()

    # Summary
    print("\n" + "="*50)
    print("TEST SUMMARY")
    print("="*50)
    passed = sum(1 for r in results if r.success)
    failed = len(results) - passed
    print(f"Total: {len(results)} | Passed: {passed} | Failed: {failed}")

    for r in results:
        status = "✓" if r.success else "✗"
        print(f"  {status} {r.test_name}")

    if failed > 0:
        print("\nFailures:")
        for r in results:
            if not r.success:
                print(f"  - {r.test_name}: {r.error}")

    return failed == 0


if __name__ == "__main__":
    success = asyncio.run(main())
    exit(0 if success else 1)