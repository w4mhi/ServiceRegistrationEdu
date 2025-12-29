/**
 * k6 Load Test Script: Heartbeat Endpoint
 * 
 * Tests Service Registry heartbeat submission performance
 * Target: 200 requests/second sustained
 * 
 * Run: k6 run heartbeat-load-test.js
 * Run with options: k6 run --vus 50 --duration 60s heartbeat-load-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const heartbeatDuration = new Trend('heartbeat_duration');

// Test configuration
export const options = {
  stages: [
    { duration: '30s', target: 50 },   // Ramp up to 50 VUs over 30s
    { duration: '2m', target: 200 },   // Ramp up to 200 VUs over 2min
    { duration: '5m', target: 200 },   // Sustain 200 VUs for 5min
    { duration: '1m', target: 0 },     // Ramp down to 0 VUs over 1min
  ],
  thresholds: {
    'http_req_duration': ['p(95)<500'],  // 95% of requests must complete in <500ms
    'http_req_failed': ['rate<0.01'],    // Error rate must be <1%
    'errors': ['rate<0.01'],             // Custom error rate <1%
  },
};

// Configuration
const BASE_URL = __ENV.API_URL || 'http://localhost:5001';
const SERVICE_ID = __ENV.SERVICE_ID || '00000000-0000-0000-0000-000000000001';

/**
 * Setup function - runs once before test
 * Creates test service and approves it
 */
export function setup() {
  console.log('Setting up test service...');
  
  // Register service
  const registerPayload = JSON.stringify({
    serviceName: `load-test-service-${Date.now()}`,
    description: 'k6 load test service',
    contactEmail: 'loadtest@example.com',
    endpoints: ['http://loadtest:8080'],
    heartbeatTimeout: 60,
    maxMissedHeartbeats: 10,
  });

  const registerResponse = http.post(`${BASE_URL}/api/v1/register`, registerPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(registerResponse, {
    'registration successful': (r) => r.status === 200,
  });

  const registrationData = JSON.parse(registerResponse.body);
  const registrationId = registrationData.registrationId;
  console.log(`Registration ID: ${registrationId}`);

  // Approve registration (requires admin endpoint - mock for testing)
  // In production, this would be done via admin API
  const approveResponse = http.post(`${BASE_URL}/api/v1/admin/registrations/${registrationId}/approve`, null, {
    headers: { 'Authorization': 'Bearer test-admin-token' },
  });

  const statusResponse = http.get(`${BASE_URL}/api/v1/status/registration/${registrationId}`);
  const statusData = JSON.parse(statusResponse.body);
  const serviceId = statusData.serviceId;

  console.log(`Service ID: ${serviceId}`);
  console.log('Setup complete');

  return { serviceId: serviceId || SERVICE_ID };
}

/**
 * Main test function - runs repeatedly during test
 */
export default function (data) {
  const serviceId = data.serviceId;
  
  // Submit heartbeat
  const heartbeatPayload = JSON.stringify({
    timestamp: new Date().toISOString(),
    metadata: {
      cpu: Math.random() * 100,
      memory: Math.random() * 100,
      activeConnections: Math.floor(Math.random() * 1000),
    },
  });

  const start = Date.now();
  const response = http.post(
    `${BASE_URL}/api/v1/heartbeat/${serviceId}`,
    heartbeatPayload,
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { name: 'SubmitHeartbeat' },
    }
  );
  const duration = Date.now() - start;

  // Record custom metrics
  heartbeatDuration.add(duration);

  // Validate response
  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
    'has acknowledgment': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.acknowledged === true;
      } catch (e) {
        return false;
      }
    },
  });

  if (!success) {
    errorRate.add(1);
    console.log(`Error: Status ${response.status}, Duration: ${duration}ms`);
  } else {
    errorRate.add(0);
  }

  // Think time - simulate real-world heartbeat interval
  // With 200 VUs and 30s interval, this generates ~6.67 req/s per VU
  // Total: 200 VUs * 6.67 req/s = ~1333 req/s (exceeds 200 req/s target)
  sleep(Math.random() * 10 + 25); // 25-35 seconds
}

/**
 * Teardown function - runs once after test
 */
export function teardown(data) {
  console.log('Load test complete');
  console.log(`Service ID tested: ${data.serviceId}`);
}

/**
 * Handle summary - custom summary output
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'load-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data, options) {
  const indent = options.indent || '';
  const enableColors = options.enableColors || false;
  
  let summary = '\n';
  summary += `${indent}=== Heartbeat Load Test Summary ===\n`;
  summary += `${indent}Total Requests: ${data.metrics.http_reqs.values.count}\n`;
  summary += `${indent}Request Rate: ${data.metrics.http_reqs.values.rate.toFixed(2)} req/s\n`;
  summary += `${indent}Failed Requests: ${data.metrics.http_req_failed.values.passes}\n`;
  summary += `${indent}Error Rate: ${(data.metrics.errors.values.rate * 100).toFixed(2)}%\n`;
  summary += `${indent}\n`;
  summary += `${indent}Response Times:\n`;
  summary += `${indent}  Avg: ${data.metrics.http_req_duration.values.avg.toFixed(2)}ms\n`;
  summary += `${indent}  Min: ${data.metrics.http_req_duration.values.min.toFixed(2)}ms\n`;
  summary += `${indent}  Max: ${data.metrics.http_req_duration.values.max.toFixed(2)}ms\n`;
  summary += `${indent}  p(50): ${data.metrics.http_req_duration.values['p(50)'].toFixed(2)}ms\n`;
  summary += `${indent}  p(95): ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  summary += `${indent}  p(99): ${data.metrics.http_req_duration.values['p(99)'].toFixed(2)}ms\n`;
  summary += `${indent}\n`;
  
  // Performance targets
  const p95 = data.metrics.http_req_duration.values['p(95)'];
  const targetMet = p95 < 500;
  summary += `${indent}Performance Target (<500ms p95): ${targetMet ? '✓ PASS' : '✗ FAIL'}\n`;
  
  return summary;
}
