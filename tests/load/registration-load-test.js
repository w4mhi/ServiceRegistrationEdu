/**
 * k6 Load Test Script: Registration Endpoint
 * 
 * Tests Service Registry registration submission performance
 * Target: <2000ms response time
 * 
 * Run: k6 run registration-load-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const registrationDuration = new Trend('registration_duration');
const registrationCount = new Counter('registrations');

// Test configuration
export const options = {
  stages: [
    { duration: '1m', target: 10 },    // Ramp up to 10 VUs over 1min
    { duration: '3m', target: 10 },    // Sustain 10 VUs for 3min
    { duration: '30s', target: 0 },    // Ramp down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<2000'], // 95% of requests must complete in <2s
    'http_req_failed': ['rate<0.05'],    // Error rate must be <5%
    'errors': ['rate<0.05'],
  },
};

// Configuration
const BASE_URL = __ENV.API_URL || 'http://localhost:5001';

/**
 * Generate unique service name for each registration
 */
function generateServiceName() {
  const timestamp = Date.now();
  const random = Math.floor(Math.random() * 10000);
  return `load-test-service-${timestamp}-${random}`;
}

/**
 * Generate valid registration payload
 */
function generateRegistrationPayload() {
  return {
    serviceName: generateServiceName(),
    description: 'Automated load test service registration',
    contactEmail: `loadtest-${Date.now()}@example.com`,
    endpoints: [
      `http://service${Math.floor(Math.random() * 1000)}:8080`,
      `http://service${Math.floor(Math.random() * 1000)}:8081`,
    ],
    heartbeatTimeout: 30 + Math.floor(Math.random() * 60), // 30-90 seconds
    maxMissedHeartbeats: 3 + Math.floor(Math.random() * 7), // 3-10 missed
  };
}

/**
 * Main test function - runs repeatedly during test
 */
export default function () {
  const payload = JSON.stringify(generateRegistrationPayload());
  
  const start = Date.now();
  const response = http.post(
    `${BASE_URL}/api/v1/register`,
    payload,
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { name: 'RegisterService' },
    }
  );
  const duration = Date.now() - start;

  // Record custom metrics
  registrationDuration.add(duration);

  // Validate response
  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 2000ms': (r) => r.timings.duration < 2000,
    'has registration ID': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.registrationId && body.registrationId.length > 0;
      } catch (e) {
        return false;
      }
    },
    'status is pending': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.status === 'pending';
      } catch (e) {
        return false;
      }
    },
  });

  if (!success) {
    errorRate.add(1);
    console.log(`Error: Status ${response.status}, Duration: ${duration}ms`);
    if (response.body) {
      console.log(`Response body: ${response.body}`);
    }
  } else {
    errorRate.add(0);
    registrationCount.add(1);
  }

  // Think time - simulate user behavior
  sleep(Math.random() * 3 + 2); // 2-5 seconds between registrations
}

/**
 * Handle summary - custom summary output
 */
export function handleSummary(data) {
  const summary = {
    'stdout': textSummary(data),
    'registration-load-results.json': JSON.stringify(data),
  };
  
  return summary;
}

function textSummary(data) {
  let summary = '\n';
  summary += '=== Registration Load Test Summary ===\n';
  summary += `Total Registrations: ${data.metrics.registrations.values.count}\n`;
  summary += `Request Rate: ${data.metrics.http_reqs.values.rate.toFixed(2)} req/s\n`;
  summary += `Failed Requests: ${data.metrics.http_req_failed.values.passes}\n`;
  summary += `Error Rate: ${(data.metrics.errors.values.rate * 100).toFixed(2)}%\n`;
  summary += '\n';
  summary += 'Response Times:\n';
  summary += `  Avg: ${data.metrics.http_req_duration.values.avg.toFixed(2)}ms\n`;
  summary += `  Min: ${data.metrics.http_req_duration.values.min.toFixed(2)}ms\n`;
  summary += `  Max: ${data.metrics.http_req_duration.values.max.toFixed(2)}ms\n`;
  summary += `  p(50): ${data.metrics.http_req_duration.values['p(50)'].toFixed(2)}ms\n`;
  summary += `  p(95): ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  summary += `  p(99): ${data.metrics.http_req_duration.values['p(99)'].toFixed(2)}ms\n`;
  summary += '\n';
  
  // Performance targets
  const p95 = data.metrics.http_req_duration.values['p(95)'];
  const targetMet = p95 < 2000;
  summary += `Performance Target (<2000ms p95): ${targetMet ? '✓ PASS' : '✗ FAIL'}\n`;
  
  return summary;
}
