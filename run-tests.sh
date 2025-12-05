#!/bin/bash

# I4.0 Sharp Messaging - Test Runner Script
# Startet optional den MQTT Broker und führt Tests aus

set -e

# Farben für Output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔══════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  I4.0 Sharp Messaging - Test Runner                 ║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════════════════╝${NC}"
echo ""

# Prüfe ob MQTT Broker läuft
check_mqtt_broker() {
    echo -e "${YELLOW}⏳ Prüfe MQTT Broker auf localhost:1883...${NC}"
    
    if timeout 2 bash -c 'cat < /dev/null > /dev/tcp/localhost/1883' 2>/dev/null; then
        echo -e "${GREEN}✓ MQTT Broker ist erreichbar${NC}"
        return 0
    else
        echo -e "${RED}✗ MQTT Broker ist nicht erreichbar${NC}"
        return 1
    fi
}

# Starte MQTT Broker mit Docker Compose
start_mqtt_broker() {
    echo -e "${YELLOW}⏳ Starte MQTT Broker mit Docker Compose...${NC}"
    
    if [ -f "../playground-v3/docker-compose.yml" ]; then
        cd ../playground-v3
        docker-compose up -d mosquitto
        cd - > /dev/null
        
        echo -e "${YELLOW}⏳ Warte 3 Sekunden auf Broker-Start...${NC}"
        sleep 3
        
        if check_mqtt_broker; then
            echo -e "${GREEN}✓ MQTT Broker erfolgreich gestartet${NC}"
            return 0
        else
            echo -e "${RED}✗ MQTT Broker konnte nicht gestartet werden${NC}"
            return 1
        fi
    else
        echo -e "${RED}✗ docker-compose.yml nicht gefunden${NC}"
        return 1
    fi
}

# Hauptmenü
echo "Wähle eine Test-Option:"
echo "  1) Unit Tests (kein MQTT Broker erforderlich)"
echo "  2) Integrationstests (MQTT Broker erforderlich)"
echo "  3) Alle Tests"
echo "  4) MQTT Broker starten und Integrationstests ausführen"
echo ""
read -p "Auswahl [1-4]: " choice

case $choice in
    1)
        echo -e "${BLUE}▶ Führe Unit Tests aus...${NC}"
        cd I40Sharp.Messaging.Tests
        dotnet test --filter "FullyQualifiedName!~Integration" --logger "console;verbosity=normal"
        ;;
    2)
        echo -e "${BLUE}▶ Führe Integrationstests aus...${NC}"
        if ! check_mqtt_broker; then
            echo -e "${YELLOW}⚠ Bitte starte den MQTT Broker manuell oder wähle Option 4${NC}"
            exit 1
        fi
        cd I40Sharp.Messaging.Tests
        dotnet test --filter "FullyQualifiedName~Integration" --logger "console;verbosity=normal"
        ;;
    3)
        echo -e "${BLUE}▶ Führe alle Tests aus...${NC}"
        if ! check_mqtt_broker; then
            echo -e "${YELLOW}⚠ MQTT Broker nicht erreichbar, Integrationstests werden übersprungen${NC}"
            cd I40Sharp.Messaging.Tests
            dotnet test --filter "FullyQualifiedName!~Integration" --logger "console;verbosity=normal"
        else
            cd I40Sharp.Messaging.Tests
            dotnet test --logger "console;verbosity=normal"
        fi
        ;;
    4)
        start_mqtt_broker
        echo -e "${BLUE}▶ Führe Integrationstests aus...${NC}"
        cd I40Sharp.Messaging.Tests
        dotnet test --filter "FullyQualifiedName~Integration" --logger "console;verbosity=normal"
        ;;
    *)
        echo -e "${RED}✗ Ungültige Auswahl${NC}"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  Tests abgeschlossen!                                ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════╝${NC}"
