#!/usr/bin/env python3
"""
LLM-Generated Test: VanPatten AI Router - Auto Mode Test
Tests the vanpatten/auto virtual model which routes to all models (free + paid).
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

class VanPattenAutoTester:
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
        url = f"{self.base_url}/v1/chat/completions"
        payload = {
            "model": model,
            "messages": messages,
            "stream": stream,
            "max_tokens": 150,
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

    async def run_auto_tests(self) -> List[TestResult]:
        """Run comprehensive tests for vanpatten/auto mode."""
        results = []

        print("=== Testing vanpatten/auto Mode (All Models) ===\n")

        # Test 1: List models
        print("1. Listing available models...")
        result = await self.list_models()
        results.append(result)
        if result.success:
            models = result.response.get("data", [])
            free_models = [m for m in models if m.get("is_free")]
            paid_models = [m for m in models if not m.get("is_free")]
            print(f"   Total: {len(models)} | Free: {len(free_models)} | Paid: {len(paid_models)}")

        # Test 2: Non-streaming with auto mode
        print("\n2. Non-streaming completion with auto (all models)...")
        result = await self.chat_completion(
            model="vanpatten/auto",
            messages=[{"role": "user", "content": "Write a Python class for a simple calculator"}],
            stream=False
        )
        results.append(result)
        if result.success:
            content = result.response.get("choices", [{}])[0].get("message", {}).get("content", "")
            print(f"   SUCCESS: Generated {len(content)} chars")
            print(f"   Model used: {result.response.get('model', 'unknown')}")

        # Test 3: Streaming with auto mode
        print("\n3. Streaming completion with auto...")
        result = await self.chat_completion(
            model="vanpatten/auto",
            messages=[{"role": "user", "content": "Explain Python decorators in 2 sentences"}],
            stream=True
        )
        results.append(result)
        if result.success:
            print(f"   SUCCESS: Streamed {len(result.response.get('content', ''))} chars")

        # Test 4: Compare with auto-free
        print("\n4. Comparing auto vs auto-free for same prompt...")
        prompt = "What is Python?"
        
        auto_result = await self.chat_completion(
            model="vanpatten/auto",
            messages=[{"role": "user", "content": prompt}],
            stream=False
        )
        free_result = await self.chat_completion(
            model="vanpatten/auto-free",
            messages=[{"role": "user", "content": prompt}],
            stream=False
        )
        
        results.append(auto_result)
        results.append(free_result)
        
        if auto_result.success and free_result.success:
            auto_model = auto_result.response.get('model', 'unknown')
            free_model = free_result.response.get('model', 'unknown')
            print(f"   auto model: {auto_model}")
            print(f"   auto-free model: {free_model}")
            print(f"   Different models used: {auto_model != free_model}")

        return results


async def main():
    async with VanPattenAutoTester() as tester:
        results = await tester.run_auto_tests()

    print("\n" + "="*50)
    print("TEST SUMMARY")
    print("="*50)
    passed = sum(1 for r in results if r.success)
    failed = len(results) - passed
    print(f"Total: {len(results)} | Passed: {passed} | Failed: {failed}")

    for r in results:
        status = "✓" if r.success else "✗"
        print(f"  {status} {r.test_name}")

    return failed == 0


if __name__ == "__main__":
    success = asyncio.run(main())
    exit(0 if success else 1)