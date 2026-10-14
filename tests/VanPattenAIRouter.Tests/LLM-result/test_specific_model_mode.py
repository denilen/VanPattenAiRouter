#!/usr/bin/env python3
"""
LLM-Generated Test: VanPatten AI Router - Specific Model Mode Test
Tests the vanpatten/{model-name} virtual model which routes to a specific model
with failover to other providers that have the same model.
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

# Models to test - these should exist on multiple KiloCode accounts
TEST_MODELS = [
    "google/gemini-2.0-flash-exp:free",
    "meta-llama/llama-3.1-8b-instruct:free",
    "mistralai/mistral-7b-instruct:free",
]

@dataclass
class TestResult:
    test_name: str
    success: bool
    response: Dict[str, Any]
    error: str = ""

class VanPattenSpecificModelTester:
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
            "max_tokens": 100,
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

    async def get_admin_providers(self) -> TestResult:
        url = f"{self.base_url}/admin/providers"
        try:
            async with self.session.get(url) as response:
                data = await response.json()
                return TestResult(
                    test_name="admin_providers",
                    success=response.status == 200,
                    response=data
                )
        except Exception as e:
            return TestResult(
                test_name="admin_providers",
                success=False,
                response={},
                error=str(e)
            )

    async def test_specific_model(self, model_name: str) -> List[TestResult]:
        """Test a specific model with failover."""
        results = []
        virtual_model = f"vanpatten/{model_name}"
        
        print(f"\n=== Testing Specific Model: {virtual_model} ===")

        # Test 1: Check if model exists on multiple providers via admin
        print("1. Checking provider availability via admin...")
        result = await self.get_admin_providers()
        results.append(result)
        if result.success:
            providers = result.response
            kilocode_providers = [p for p in providers if "KiloCode" in p.get("name", "")]
            print(f"   Found {len(kilocode_providers)} KiloCode providers")
            for p in kilocode_providers:
                models = p.get("models", [])
                has_model = any(m.get("model_name") == model_name for m in models)
                status = "✓" if has_model else "✗"
                print(f"   {status} {p['name']}: {len(models)} models, has {model_name}: {has_model}")

        # Test 2: Non-streaming request
        print(f"\n2. Non-streaming request to {model_name}...")
        result = await self.chat_completion(
            model=f"vanpatten/{model_name}",
            messages=[{"role": "user", "content": "Write a one-line Python lambda to square a number"}],
            stream=False
        )
        results.append(result)
        if result.success:
            content = result.response.get("choices", [{}])[0].get("message", {}).get("content", "")
            actual_model = result.response.get("model", "unknown")
            print(f"   SUCCESS: Response from {actual_model}")
            print(f"   Content preview: {content[:80]}...")
        else:
            print(f"   FAILED: {result.error}")

        # Test 3: Streaming request
        print(f"\n3. Streaming request to {model_name}...")
        result = await self.chat_completion(
            model=f"vanpatten/{model_name}",
            messages=[{"role": "user", "content": "Count to 5 in Python"}],
            stream=True
        )
        results.append(result)
        if result.success:
            print(f"   SUCCESS: Streamed {len(result.response.get('content', ''))} chars")
        else:
            print(f"   FAILED: {result.error}")

        # Test 4: Multiple requests to verify consistent failover
        print(f"\n4. Multiple requests (testing consistent routing)...")
        for i in range(3):
            result = await self.chat_completion(
                model=f"vanpatten/{model_name}",
                messages=[{"role": "user", "content": f"Simple Python test {i+1}"}],
                stream=False
            )
            results.append(result)
            status = "✓" if result.success else "✗"
            actual_model = result.response.get("model", "unknown") if result.success else "N/A"
            print(f"   {status} Request {i+1}: {actual_model}")

        return results

    async def run_all_specific_model_tests(self) -> List[TestResult]:
        """Run tests for all configured models."""
        all_results = []
        
        print("="*60)
        print("Testing vanpatten/{model-name} Specific Model Mode")
        print("="*60)

        # First check what models are available
        await self.list_models()

        for model in TEST_MODELS:
            model_results = await self.test_specific_model(model)
            all_results.extend(model_results)

        return all_results


async def main():
    async with VanPattenSpecificModelTester() as tester:
        results = await tester.run_all_specific_model_tests()

    print("\n" + "="*60)
    print("TEST SUMMARY")
    print("="*60)
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