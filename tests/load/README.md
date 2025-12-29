# Load Testing Guide

This directory contains k6 load testing scripts for the Service Registry API.

## Prerequisites

Install k6 from Grafana Labs:

```bash
# macOS
brew install k6

# Ubuntu/Debian
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# Windows
choco install k6

# Docker
docker pull grafana/k6
```

Documentation: https://k6.io/docs/

## Test Scripts

### 1. Heartbeat Load Test (`heartbeat-load-test.js`)

Tests heartbeat submission performance under sustained load.

**Performance Target**: 200 requests/second with p95 < 500ms

**Test Stages**:
- Ramp up to 50 VUs over 30 seconds
- Ramp up to 200 VUs over 2 minutes
- Sustain 200 VUs for 5 minutes
- Ramp down over 1 minute

**Run**:
```bash
# Default configuration
k6 run heartbeat-load-test.js

# Custom API URL
k6 run --env API_URL=http://api.example.com heartbeat-load-test.js

# Custom VUs and duration
k6 run --vus 100 --duration 180s heartbeat-load-test.js

# With Docker
docker run --rm -i grafana/k6 run - < heartbeat-load-test.js
```

**Metrics Collected**:
- `http_req_duration`: Request latency (target: p95 < 500ms)
- `http_req_failed`: Failed request rate (target: < 1%)
- `heartbeat_duration`: Custom metric for heartbeat processing time
- `errors`: Custom error rate

### 2. Registration Load Test (`registration-load-test.js`)

Tests service registration endpoint performance.

**Performance Target**: p95 < 2000ms

**Test Stages**:
- Ramp up to 10 VUs over 1 minute
- Sustain 10 VUs for 3 minutes
- Ramp down over 30 seconds

**Run**:
```bash
# Default configuration
k6 run registration-load-test.js

# Custom API URL
k6 run --env API_URL=http://api.example.com registration-load-test.js

# Increase load
k6 run --vus 20 --duration 300s registration-load-test.js
```

**Metrics Collected**:
- `http_req_duration`: Request latency (target: p95 < 2000ms)
- `http_req_failed`: Failed request rate (target: < 5%)
- `registration_duration`: Custom metric for registration processing time
- `registrations`: Counter for successful registrations

## Test Scenarios

### Scenario 1: Baseline Performance

Test API under normal load conditions:

```bash
# Run heartbeat test with moderate load
k6 run --vus 50 --duration 120s heartbeat-load-test.js

# Run registration test with low load
k6 run --vus 5 --duration 60s registration-load-test.js
```

### Scenario 2: Stress Test

Push API to limits to identify breaking points:

```bash
# High heartbeat load
k6 run --vus 500 --duration 300s heartbeat-load-test.js

# Increased registration load
k6 run --vus 50 --duration 180s registration-load-test.js
```

### Scenario 3: Soak Test

Test system stability over extended period:

```bash
# 1-hour heartbeat soak test
k6 run --vus 100 --duration 3600s heartbeat-load-test.js

# 30-minute registration soak test
k6 run --vus 10 --duration 1800s registration-load-test.js
```

### Scenario 4: Spike Test

Test system resilience to sudden traffic spikes:

Create `spike-test.js`:
```javascript
export const options = {
  stages: [
    { duration: '1m', target: 10 },   // Normal load
    { duration: '10s', target: 500 }, // Spike!
    { duration: '1m', target: 500 },  // Sustain spike
    { duration: '10s', target: 10 },  // Back to normal
    { duration: '1m', target: 10 },   // Recovery
  ],
};
```

## Results Analysis

### Output Files

k6 generates JSON results files:
- `load-test-results.json` (heartbeat test)
- `registration-load-results.json` (registration test)

### Key Metrics

**Response Time**:
- `p(50)`: Median response time
- `p(95)`: 95th percentile - **primary SLA metric**
- `p(99)`: 99th percentile - worst-case performance
- `avg`: Average response time
- `max`: Maximum response time

**Throughput**:
- `http_reqs`: Total requests
- `http_reqs{rate}`: Requests per second

**Errors**:
- `http_req_failed`: Failed HTTP requests
- `errors`: Custom error rate

### Performance Targets

| Endpoint | Target | Threshold |
|----------|--------|-----------|
| Heartbeat | p95 < 500ms | CRITICAL |
| Registration | p95 < 2000ms | HIGH |
| Status Query | p95 < 1000ms | MEDIUM |

### Interpreting Results

**PASS Criteria**:
- ✓ p95 response time meets target
- ✓ Error rate < 5% for registration, < 1% for heartbeat
- ✓ No HTTP 500 errors
- ✓ Throughput meets requirement (200 req/s for heartbeat)

**FAIL Indicators**:
- ✗ p95 > target threshold
- ✗ High error rate (> 5%)
- ✗ HTTP 500 errors present
- ✗ Request rate drops during sustained load

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: Load Tests

on:
  schedule:
    - cron: '0 2 * * *'  # Daily at 2 AM
  workflow_dispatch:

jobs:
  load-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Install k6
        run: |
          sudo apt-get update
          sudo apt-get install -y k6
      
      - name: Run heartbeat load test
        run: |
          k6 run --env API_URL=${{ secrets.API_URL }} \
            --out json=heartbeat-results.json \
            tests/load/heartbeat-load-test.js
      
      - name: Check performance thresholds
        run: |
          # Fail build if thresholds not met
          if grep -q '"failed":true' heartbeat-results.json; then
            echo "Load test thresholds failed"
            exit 1
          fi
      
      - name: Upload results
        uses: actions/upload-artifact@v3
        with:
          name: load-test-results
          path: |
            heartbeat-results.json
            registration-load-results.json
```

## Cloud Load Testing

### k6 Cloud

Run tests from cloud infrastructure for geographic distribution:

```bash
# Login to k6 cloud
k6 login cloud

# Run test in cloud
k6 cloud heartbeat-load-test.js

# View results at https://app.k6.io
```

### Distributed Execution

For very high load, distribute test across multiple machines:

```bash
# Machine 1
k6 run --vus 100 heartbeat-load-test.js

# Machine 2
k6 run --vus 100 heartbeat-load-test.js

# Machine 3
k6 run --vus 100 heartbeat-load-test.js

# Total: 300 VUs distributed
```

## Monitoring During Tests

While running load tests, monitor:

1. **Application Logs**:
   ```bash
   kubectl logs -f deployment/service-registry-api -n production
   ```

2. **Database Performance**:
   ```sql
   -- PostgreSQL active queries
   SELECT pid, usename, state, query 
   FROM pg_stat_activity 
   WHERE state != 'idle';
   ```

3. **System Resources**:
   ```bash
   # CPU and memory
   kubectl top pods -n production
   
   # HPA status
   kubectl get hpa -n production
   ```

4. **Performance Metrics** (if Prometheus configured):
   - HTTP request duration
   - Database query time
   - Queue depth
   - Error rates

## Troubleshooting

### High Error Rates

If error rate exceeds threshold:
1. Check API logs for exceptions
2. Verify database connection pool settings
3. Check rate limiting configuration
4. Review resource limits (CPU/memory)

### Slow Response Times

If p95 > target:
1. Check database query performance (EXPLAIN ANALYZE)
2. Review database indexes
3. Check for N+1 queries
4. Verify adequate resources (scale up/out)
5. Review middleware performance (logging, auth)

### Connection Failures

If HTTP connection errors:
1. Check max connections limit
2. Verify load balancer configuration
3. Review timeout settings
4. Check DNS resolution

## Best Practices

1. **Baseline First**: Run tests in development before production
2. **Incremental Load**: Ramp up gradually, don't spike immediately
3. **Monitor Resources**: Watch CPU, memory, connections during test
4. **Clean Data**: Clear test data between runs
5. **Consistent Environment**: Test in production-like environment
6. **Document Results**: Save results with git SHA and environment details

## References

- k6 Documentation: https://k6.io/docs/
- k6 Cloud: https://app.k6.io
- Performance Testing Guide: https://k6.io/docs/testing-guides/
- Metrics Reference: https://k6.io/docs/using-k6/metrics/
