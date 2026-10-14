## LLM Test Programs

[Русский](README-RU.md) | **English**

This directory contains Python test scripts generated to comprehensively test the VanPatten AI Router's functionality across all three virtual provider modes and edge cases.

## Test Scripts

| Script                            | Description |
|-----------------------------------|-------------|
| `test_auto_free_mode.py`          | Tests `vanpatten/auto-free` mode - routes only to free models |
| `test_auto_mode.py`               | Tests `vanpatten/auto` mode - routes to all models (free + paid) |
| `test_specific_model_mode.py`     | Tests `vanpatten/{model-name}` mode - specific model with failover |
| `test_rate_limit_and_failover.py` | Comprehensive rate limiting and failover tests |
| `test_stream_stability.py`        | Stream Stability Stress Test |

## Running the Tests

### Prerequisites
```bash
pip install aiohttp
```

### Environment Variables
```bash
export VANPATTEN_API_URL="http://localhost:10080"
export VANPATTEN_API_KEY=""  # Optional, for authenticated endpoints
```

### Run Individual Tests
```bash
python test_auto_free_mode.py
python test_auto_mode.py
python test_specific_model_mode.py
python test_rate_limit_and_failover.py
```

### Run All Tests
```bash
for test in test_*.py; do
    echo "Running $test..."
    python "$test"
    echo "---"
done
```

## Test Coverage

Each test script covers:

### `test_auto_free_mode.py`
- ✅ Listing available free models
- ✅ Non-streaming completion with auto-free
- ✅ Streaming completion with auto-free
- ✅ Multiple requests to test failover
- ✅ Edge cases (minimal requests)

### `test_auto_mode.py`
- ✅ Listing all models (free + paid)
- ✅ Non-streaming completion with auto
- ✅ Streaming completion with auto
- ✅ Comparison with auto-free mode

### `test_specific_model_mode.py`
- ✅ Admin provider check for model availability
- ✅ Non-streaming request to specific model
- ✅ Streaming request to specific model
- ✅ Multiple requests to verify consistent failover
- ✅ Tests multiple models (gemini, llama, mistral)

### `test_rate_limit_and_failover.py`
- ✅ Rate limit statistics checking
- ✅ Burst request testing
- ✅ Failover behavior verification
- ✅ Streaming failover
- ✅ Token estimation accuracy
- ✅ Concurrent request handling (model refresh non-interruption)

### `test_stream_stability.py`
- ✅ Long-running streaming (5+ minutes per scenario, 15+ minutes total)
- ✅ Complex analytical prompts requiring extensive generation
- ✅ Three sequential scenarios: `vanpatten/auto`, `vanpatten/auto-free`, `vanpatten/auto`
- ✅ Warm-up request before main stress test
- ✅ Progress reporting every 50 chunks
- ✅ Disconnect detection (connection closed prematurely)
- ✅ Empty stream detection (no content received)
- ✅ Slow chunk detection (gaps > 5 seconds between chunks)
- ✅ Time to First Chunk (TTFC) measurement
- ✅ Throughput measurement (chars/second)
- ✅ Retry logic with exponential backoff (2 retries)
- ✅ JSON result saving with timestamp
- ✅ Summary comparison across all scenarios
- ✅ Provider/model tracking from response metadata
- ✅ Memory/performance monitoring

## Expected Results

All tests should pass when:
1. VanPatten AI Router is running on `http://localhost:10080`
2. At least one KiloCode provider is configured and active
3. Rate limits are not exceeded during test execution

## Notes

- Tests use `vanpatten/auto-free` by default as it's the safest mode
- Specific model tests require the model to exist on at least one active provider
- Rate limit tests may fail if provider limits are exceeded during testing
- Streaming tests verify proper SSE chunk handling