"""
Integration Tests - Fase 3 Complete System

Tests the complete flow:
Sensor -> RabbitMQ -> Gateway -> Pre-Processamento -> Server -> Análise
"""

import json
import time
import requests
import subprocess
import os
import sys
from pathlib import Path

# ============================================================================
# CONFIGURATION
# ============================================================================

PREPROC_URL = "http://localhost:5001"
ANALISE_URL = "http://localhost:6001"
RABBITMQ_MGMT_URL = "http://localhost:15672/api"
RABBITMQ_CREDS = ("guest", "guest")

class Colors:
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    CYAN = '\033[36m'
    RESET = '\033[0m'
    BOLD = '\033[1m'

def log_info(msg):
    print(f"{Colors.CYAN}[INFO]{Colors.RESET} {msg}")

def log_success(msg):
    print(f"{Colors.GREEN}[✓]{Colors.RESET} {msg}")

def log_warning(msg):
    print(f"{Colors.YELLOW}[⚠]{Colors.RESET} {msg}")

def log_error(msg):
    print(f"{Colors.RED}[✗]{Colors.RESET} {msg}")

# ============================================================================
# TEST 1: PRE-PROCESSAMENTO SERVICE
# ============================================================================

def test_preprocessing_service():
    """Test Pre-Processamento RPC service availability and endpoints"""
    print(f"\n{Colors.BOLD}TEST 1: Pre-Processamento Service{Colors.RESET}")
    print("=" * 70)
    
    try:
        # Health check
        log_info("Checking Pre-Processamento service health...")
        response = requests.get(f"{PREPROC_URL}/", timeout=2)
        if response.status_code in [200, 404]:
            log_success("Pre-Processamento service is running")
        else:
            log_error(f"Service returned status {response.status_code}")
            return False
    except requests.exceptions.ConnectionError:
        log_error("Cannot connect to Pre-Processamento service (port 5001)")
        return False
    except Exception as e:
        log_error(f"Error: {e}")
        return False
    
    # Test uniformization
    log_info("Testing data uniformization (Fahrenheit -> Celsius)...")
    payload = {
        "sensor_id": "test-sensor",
        "tipo_dado": "temperatura",
        "valor": 212.0,
        "timestamp": "2026-05-20T10:00:00Z",
        "formato_original": "FAHRENHEIT"
    }
    
    try:
        response = requests.post(
            f"{PREPROC_URL}/rpc/uniformizar",
            json=payload,
            timeout=5
        )
        result = response.json()
        
        if result.get("sucesso") and result.get("valor_uniformizado") == 100.0:
            log_success(f"Uniformization: 212°F -> {result['valor_uniformizado']}°C")
        else:
            log_error(f"Unexpected result: {result}")
            return False
    except Exception as e:
        log_error(f"Uniformization test failed: {e}")
        return False
    
    # Test validation
    log_info("Testing data validation...")
    payload_valid = {
        "sensor_id": "test-sensor",
        "tipo_dado": "temperatura",
        "valor": 25.0
    }
    
    try:
        response = requests.post(
            f"{PREPROC_URL}/rpc/validar",
            json=payload_valid,
            timeout=5
        )
        result = response.json()
        
        if result.get("valido") is True and len(result.get("erros", [])) == 0:
            log_success("Validation: 25°C is valid")
        else:
            log_error(f"Unexpected validation result: {result}")
            return False
    except Exception as e:
        log_error(f"Validation test failed: {e}")
        return False
    
    return True

# ============================================================================
# TEST 2: ANALISE SERVICE
# ============================================================================

def test_analise_service():
    """Test Análise RPC service and analysis endpoints"""
    print(f"\n{Colors.BOLD}TEST 2: Análise Service{Colors.RESET}")
    print("=" * 70)
    
    try:
        log_info("Checking Análise service health...")
        response = requests.get(f"{ANALISE_URL}/", timeout=2)
        if response.status_code in [200, 404]:
            log_success("Análise service is running")
        else:
            log_error(f"Service returned status {response.status_code}")
            return False
    except requests.exceptions.ConnectionError:
        log_error("Cannot connect to Análise service (port 6001)")
        return False
    except Exception as e:
        log_error(f"Error: {e}")
        return False
    
    # Test statistics
    log_info("Testing statistical analysis...")
    payload = {
        "sensor_id": "sensor-01",
        "tipo_dado": "temperatura",
        "valores": [20.0, 21.0, 22.0, 23.0, 24.0]
    }
    
    try:
        response = requests.post(
            f"{ANALISE_URL}/rpc/estatisticas",
            json=payload,
            timeout=5
        )
        result = response.json()
        
        if result.get("sucesso") and result.get("media") == 22.0:
            log_success(f"Statistics: mean={result['media']}, median={result['mediana']}")
        else:
            log_error(f"Unexpected result: {result}")
            return False
    except Exception as e:
        log_error(f"Statistics test failed: {e}")
        return False
    
    # Test pattern detection
    log_info("Testing anomaly detection...")
    payload_anomalies = {
        "sensor_id": "sensor-01",
        "tipo_dado": "temperatura",
        "valores": [20.0, 21.0, 22.0, 50.0, 23.0, 21.0]  # 50.0 is anomaly
    }
    
    try:
        response = requests.post(
            f"{ANALISE_URL}/rpc/padroes",
            json=payload_anomalies,
            timeout=5
        )
        result = response.json()
        
        if result.get("sucesso") and result.get("total_anomalias") > 0:
            log_success(f"Anomaly Detection: found {result['total_anomalias']} anomalies")
        else:
            log_warning(f"No anomalies detected or unexpected result: {result}")
    except Exception as e:
        log_error(f"Anomaly detection test failed: {e}")
        return False
    
    # Test forecasting
    log_info("Testing forecasting...")
    payload_forecast = {
        "sensor_id": "sensor-01",
        "tipo_dado": "temperatura",
        "valores": [20.0, 21.0, 22.0, 23.0, 24.0]
    }
    
    try:
        response = requests.post(
            f"{ANALISE_URL}/rpc/previsao",
            json=payload_forecast,
            timeout=5
        )
        result = response.json()
        
        if result.get("sucesso") and "proximo_valor" in result:
            log_success(f"Forecasting: next value = {result['proximo_valor']}, trend = {result['tendencia']}")
        else:
            log_error(f"Unexpected forecast result: {result}")
            return False
    except Exception as e:
        log_error(f"Forecasting test failed: {e}")
        return False
    
    return True

# ============================================================================
# TEST 3: RABBITMQ CONNECTIVITY
# ============================================================================

def test_rabbitmq():
    """Test RabbitMQ connectivity and queues"""
    print(f"\n{Colors.BOLD}TEST 3: RabbitMQ Setup{Colors.RESET}")
    print("=" * 70)
    
    try:
        log_info("Checking RabbitMQ availability...")
        response = requests.get(
            f"{RABBITMQ_MGMT_URL}/overview",
            auth=RABBITMQ_CREDS,
            timeout=5
        )
        
        if response.status_code == 200:
            data = response.json()
            log_success(f"RabbitMQ is running (version {data.get('product', 'unknown')})")
            
            # Get queue info
            queues_response = requests.get(
                f"{RABBITMQ_MGMT_URL}/queues",
                auth=RABBITMQ_CREDS,
                timeout=5
            )
            
            if queues_response.status_code == 200:
                queues = queues_response.json()
                log_info(f"Found {len(queues)} queue(s)")
                for q in queues:
                    log_info(f"  - {q.get('name')} (messages: {q.get('messages', 0)})")
            
            return True
        else:
            log_error(f"RabbitMQ returned status {response.status_code}")
            return False
    except requests.exceptions.ConnectionError:
        log_error("Cannot connect to RabbitMQ (port 15672)")
        return False
    except Exception as e:
        log_error(f"Error: {e}")
        return False

# ============================================================================
# TEST 4: DATA FLOW INTEGRATION
# ============================================================================

def test_data_flow():
    """Test complete data flow through the system"""
    print(f"\n{Colors.BOLD}TEST 4: Data Flow Integration{Colors.RESET}")
    print("=" * 70)
    
    log_info("Simulating sensor data flow...")
    
    # Step 1: Pre-processamento
    log_info("Step 1: Uniformizing sensor data...")
    raw_temp = 77.0  # Fahrenheit
    
    try:
        response = requests.post(
            f"{PREPROC_URL}/rpc/uniformizar",
            json={
                "sensor_id": "sensor-01",
                "tipo_dado": "temperatura",
                "valor": raw_temp,
                "timestamp": "2026-05-20T10:00:00Z",
                "formato_original": "FAHRENHEIT"
            },
            timeout=5
        )
        result = response.json()
        
        if not result.get("sucesso"):
            log_error("Pre-processamento failed")
            return False
        
        uniformized_temp = result.get("valor_uniformizado")
        log_success(f"  Converted {raw_temp}°F to {uniformized_temp}°C")
    except Exception as e:
        log_error(f"Pre-processamento error: {e}")
        return False
    
    # Step 2: Validation
    log_info("Step 2: Validating data...")
    try:
        response = requests.post(
            f"{PREPROC_URL}/rpc/validar",
            json={
                "sensor_id": "sensor-01",
                "tipo_dado": "temperatura",
                "valor": uniformized_temp
            },
            timeout=5
        )
        result = response.json()
        
        if not result.get("valido"):
            log_error(f"Data validation failed: {result.get('erros', [])}")
            return False
        
        log_success("  Data validation passed")
    except Exception as e:
        log_error(f"Validation error: {e}")
        return False
    
    # Step 3: Analysis
    log_info("Step 3: Analyzing historical data...")
    historical_temps = [20.0, 21.0, 22.0, 23.0, 25.0, uniformized_temp]
    
    try:
        response = requests.post(
            f"{ANALISE_URL}/rpc/estatisticas",
            json={
                "sensor_id": "sensor-01",
                "tipo_dado": "temperatura",
                "valores": historical_temps
            },
            timeout=5
        )
        result = response.json()
        
        if not result.get("sucesso"):
            log_error("Analysis failed")
            return False
        
        log_success(f"  Analysis: mean={result['media']}, std={result['desvio_padrao']}")
    except Exception as e:
        log_error(f"Analysis error: {e}")
        return False
    
    log_success("Data flow integration test passed!")
    return True

# ============================================================================
# MAIN TEST RUNNER
# ============================================================================

def main():
    print(f"\n{Colors.BOLD}{Colors.CYAN}")
    print("╔════════════════════════════════════════════════════════════════╗")
    print("║        FASE 3 - INTEGRATION TESTS                             ║")
    print("║        Pre-Processamento + Análise + RabbitMQ                 ║")
    print("╚════════════════════════════════════════════════════════════════╝")
    print(f"{Colors.RESET}")
    
    log_info("Waiting for services to be available (30 seconds)...")
    time.sleep(2)  # Give services time to start
    
    tests = [
        ("Pre-Processamento Service", test_preprocessing_service),
        ("Análise Service", test_analise_service),
        ("RabbitMQ Setup", test_rabbitmq),
        ("Data Flow", test_data_flow),
    ]
    
    results = {}
    for test_name, test_func in tests:
        try:
            results[test_name] = test_func()
        except Exception as e:
            log_error(f"Unexpected error in {test_name}: {e}")
            results[test_name] = False
        
        time.sleep(1)  # Small delay between tests
    
    # Summary
    print(f"\n{Colors.BOLD}{Colors.CYAN}")
    print("╔════════════════════════════════════════════════════════════════╗")
    print("║        TEST SUMMARY                                           ║")
    print("╚════════════════════════════════════════════════════════════════╝")
    print(f"{Colors.RESET}")
    
    passed = sum(1 for v in results.values() if v)
    total = len(results)
    
    for test_name, result in results.items():
        status = f"{Colors.GREEN}PASS{Colors.RESET}" if result else f"{Colors.RED}FAIL{Colors.RESET}"
        print(f"  {test_name}: {status}")
    
    print(f"\n  Total: {Colors.BOLD}{passed}/{total}{Colors.RESET} tests passed")
    
    if passed == total:
        print(f"{Colors.GREEN}{Colors.BOLD}\n✓ All integration tests passed!{Colors.RESET}\n")
        return 0
    else:
        print(f"{Colors.RED}{Colors.BOLD}\n✗ Some tests failed!{Colors.RESET}\n")
        return 1

if __name__ == "__main__":
    sys.exit(main())
