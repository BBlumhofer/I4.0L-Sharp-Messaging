#!/bin/bash

# AAS Integration Tests Runner
# Startet die Tests mit vollständigen Action/Step Strukturen

set -e

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║    I4.0 AAS Integration Tests - Test Runner                 ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Prüfe MQTT Broker
echo -e "${YELLOW}⏳ Prüfe MQTT Broker auf localhost:1883...${NC}"
if timeout 2 bash -c 'cat < /dev/null > /dev/tcp/localhost/1883' 2>/dev/null; then
    echo -e "${GREEN}✓ MQTT Broker ist erreichbar${NC}"
else
    echo -e "${YELLOW}⚠ MQTT Broker nicht erreichbar - starte ihn mit:${NC}"
    echo "  cd ../playground-v3 && docker-compose up -d mosquitto"
    exit 1
fi

echo ""
echo -e "${BLUE}Wähle einen Test:${NC}"
echo "  1) SendActionRequest_WithCompleteAasStructure"
echo "     └─ Sendet vollständige Action mit Step, InputParameters, Scheduling"
echo ""
echo "  2) SendProposalWithScheduling_WithTimeWindows"
echo "     └─ Resource Holon antwortet mit Proposal + Scheduling-Daten"
echo ""
echo "  3) CompleteNegotiationCycle_CallForProposalToAcceptance ⭐"
echo "     └─ Multi-Agent Bidding: CFP → 2 Proposals → Best Offer Acceptance"
echo ""
echo "  4) Alle AAS Integration Tests"
echo ""
echo "  5) Alle Tests (inkl. Unit Tests)"
echo ""
read -p "Auswahl [1-5]: " choice

cd I40Sharp.Messaging.Tests

case $choice in
    1)
        echo -e "${BLUE}▶ Starte: SendActionRequest Test${NC}"
        echo ""
        echo -e "${CYAN}📊 Was passiert:${NC}"
        echo "  • ProductHolon_P24 sendet Action Request an ResourceHolon_RH2"
        echo "  • Topic: factory/actions"
        echo "  • Enthält: Step0001 mit Action001, InputParameters, Scheduling"
        echo ""
        echo -e "${YELLOW}💡 In MQTTX sehen Sie:${NC}"
        echo "  • Topic: factory/actions"
        echo "  • JSON mit vollständiger Step/Action Struktur"
        echo ""
        read -p "Drücken Sie Enter zum Starten..."
        dotnet test --filter "SendActionRequest_WithCompleteAasStructure" --logger "console;verbosity=detailed"
        ;;
    2)
        echo -e "${BLUE}▶ Starte: SendProposal Test${NC}"
        echo ""
        echo -e "${CYAN}📊 Was passiert:${NC}"
        echo "  • ResourceHolon_RH2 sendet Proposal an ProductHolon_P24"
        echo "  • Topic: factory/proposals"
        echo "  • Enthält: Scheduling (Start/End/Setup/CycleTime), Cost, Availability"
        echo ""
        echo -e "${YELLOW}💡 In MQTTX sehen Sie:${NC}"
        echo "  • Topic: factory/proposals"
        echo "  • JSON mit Scheduling-Details und Kostenschätzung"
        echo ""
        read -p "Drücken Sie Enter zum Starten..."
        dotnet test --filter "SendProposalWithScheduling" --logger "console;verbosity=detailed"
        ;;
    3)
        echo -e "${BLUE}▶ Starte: Complete Negotiation Cycle Test ⭐${NC}"
        echo ""
        echo -e "${CYAN}📊 Was passiert:${NC}"
        echo "  Phase 1: ProductHolon_P24 sendet Call for Proposal"
        echo "  Phase 2: ResourceHolon_RH2 bietet 45.0 € / 120 min"
        echo "  Phase 3: ResourceHolon_RH3 bietet 42.0 € / 140 min"
        echo "  Phase 4: Product wählt RH3 (günstigster) und sendet Acceptance"
        echo ""
        echo -e "${YELLOW}💡 In MQTTX sehen Sie:${NC}"
        echo "  • Topic: factory/negotiation"
        echo "  • 1x callForProposal"
        echo "  • 2x proposal (mit Cost + Duration)"
        echo "  • 1x acceptProposal (an Gewinner RH3)"
        echo ""
        echo -e "${GREEN}✨ Dies ist der kompletteste Test - empfohlen!${NC}"
        echo ""
        read -p "Drücken Sie Enter zum Starten..."
        dotnet test --filter "CompleteNegotiationCycle" --logger "console;verbosity=detailed"
        ;;
    4)
        echo -e "${BLUE}▶ Starte: Alle AAS Integration Tests${NC}"
        echo ""
        echo -e "${CYAN}📊 Was wird getestet:${NC}"
        echo "  ✓ Action Request mit Step/Action Struktur"
        echo "  ✓ Proposal mit Scheduling"
        echo "  ✓ Multi-Agent Negotiation Cycle"
        echo ""
        echo -e "${YELLOW}💡 Tipp: Öffnen Sie MQTTX und abonnieren Sie '#'${NC}"
        echo ""
        read -p "Drücken Sie Enter zum Starten..."
        dotnet test --filter "FullyQualifiedName~AasIntegrationTests" --logger "console;verbosity=detailed"
        ;;
    5)
        echo -e "${BLUE}▶ Starte: Alle Tests (Unit + Integration)${NC}"
        echo ""
        dotnet test --logger "console;verbosity=normal"
        ;;
    *)
        echo -e "${YELLOW}✗ Ungültige Auswahl${NC}"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  Test abgeschlossen!                                         ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${CYAN}📊 Topics in MQTTX überwachen:${NC}"
echo "  • factory/actions       - Action Requests"
echo "  • factory/proposals     - Resource Proposals"
echo "  • factory/negotiation   - Complete Bidding Process"
echo ""
echo -e "${CYAN}📝 Nächste Schritte für MAS-BT Integration:${NC}"
echo "  1. Behavior Tree Nodes erstellen (specs.json)"
echo "  2. SendMessage/WaitForMessage Nodes implementieren"
echo "  3. AskForStepExecution Node mit Action/Step Struktur"
echo "  4. ReceiveOfferMessage Node für Proposal-Handling"
