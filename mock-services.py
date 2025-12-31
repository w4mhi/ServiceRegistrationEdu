#!/usr/bin/env python3
"""
Mock HTTP servers for service endpoints (ports 8001-8007)
Each server responds to /health with a 200 OK status
"""

import json
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from datetime import datetime

class HealthCheckHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        """Suppress default logging"""
        pass

    def do_GET(self):
        if self.path == '/health' or self.path == '/':
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            
            response = {
                'status': 'healthy',
                'service': self.server.service_name,
                'timestamp': datetime.utcnow().isoformat() + 'Z',
                'version': '1.0.0'
            }
            self.wfile.write(json.dumps(response).encode())
        else:
            self.send_response(404)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({'error': 'Not found'}).encode())

def start_server(port, service_name):
    """Start a mock HTTP server on the specified port"""
    server = HTTPServer(('localhost', port), HealthCheckHandler)
    server.service_name = service_name
    print(f"✓ {service_name} listening on http://localhost:{port}")
    server.serve_forever()

if __name__ == '__main__':
    services = [
        (8001, 'payment-service'),
        (8002, 'inventory-service'),
        (8003, 'notification-service'),
        (8004, 'analytics-service'),
        (8005, 'reporting-service'),
        (8006, 'auth-service'),
        (8007, 'logging-service')
    ]
    
    print("=== Starting Mock Service Endpoints ===")
    
    threads = []
    for port, name in services:
        thread = threading.Thread(target=start_server, args=(port, name), daemon=True)
        thread.start()
        threads.append(thread)
    
    print(f"\n✓ All {len(services)} mock services started!")
    print("Press Ctrl+C to stop...\n")
    
    try:
        for thread in threads:
            thread.join()
    except KeyboardInterrupt:
        print("\n\nStopping all mock services...")
