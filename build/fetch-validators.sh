#!/usr/bin/env bash
#
# Beschafft die externen Pruefwerkzeuge fuer die Gegenpruefung.
#
# Die Anwendung selbst braucht diese Werkzeuge nicht, um zu laufen – sie fuehrt
# ihre eigenen Pruefungen immer durch (siehe docs/DECISIONS.md, ADR-0004).
# In der CI ist die Gegenpruefung dagegen verpflichtend, weil nur sie belegt,
# dass die eigene Regelimplementierung mit der Norm uebereinstimmt.
#
# Alles wird gepinnt und mit Pruefsumme verifiziert. Es wird nichts ungeprueft
# ausgefuehrt.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
TOOLS_DIR="${ROOT_DIR}/tools"

# --- Gepinnte Versionen und Pruefsummen -------------------------------------
# Mustangproject CLI: Apache-2.0. Enthaelt das CEN-Schematron fuer EN 16931
# und veraPDF fuer die PDF/A-Pruefung. Bezug ueber Maven Central.
MUSTANG_VERSION="2.24.0"
MUSTANG_URL="https://repo1.maven.org/maven2/org/mustangproject/Mustang-CLI/${MUSTANG_VERSION}/Mustang-CLI-${MUSTANG_VERSION}.jar"
MUSTANG_SHA256="e4904ffa0afdce3f5836dceb927c440a05ed5d60386fdd37e17a4b2f7652edbf"
MUSTANG_JAR="${TOOLS_DIR}/mustang/Mustang-CLI-${MUSTANG_VERSION}.jar"

log() { printf '  %s\n' "$*"; }

verify_sha256() {
    local file="$1" expected="$2" actual
    actual="$(sha256sum "${file}" | cut -d' ' -f1)"
    if [[ "${actual}" != "${expected}" ]]; then
        echo "FEHLER: Pruefsumme von ${file} stimmt nicht." >&2
        echo "  erwartet: ${expected}" >&2
        echo "  erhalten: ${actual}" >&2
        rm -f "${file}"
        exit 1
    fi
    log "Pruefsumme bestaetigt."
}

echo "Externe Pruefwerkzeuge beschaffen"

if ! command -v java >/dev/null 2>&1; then
    echo "FEHLER: Keine Java-Laufzeit gefunden. Mustang benoetigt Java 11 oder neuer." >&2
    exit 1
fi
log "Java gefunden: $(java -version 2>&1 | head -n1)"

mkdir -p "${TOOLS_DIR}/mustang"

if [[ -f "${MUSTANG_JAR}" ]]; then
    log "Mustang ${MUSTANG_VERSION} liegt bereits vor."
    verify_sha256 "${MUSTANG_JAR}" "${MUSTANG_SHA256}"
else
    log "Lade Mustang ${MUSTANG_VERSION} von Maven Central ..."
    curl --fail --silent --show-error --location --max-time 600 \
        --output "${MUSTANG_JAR}.part" "${MUSTANG_URL}"
    mv "${MUSTANG_JAR}.part" "${MUSTANG_JAR}"
    verify_sha256 "${MUSTANG_JAR}" "${MUSTANG_SHA256}"
fi

# Kurzer Funktionsnachweis: Ein Werkzeug, das sich nicht starten laesst, ist
# fuer die Gegenpruefung wertlos – das soll hier auffallen und nicht erst im Test.
if ! java -jar "${MUSTANG_JAR}" --help >/dev/null 2>&1; then
    echo "FEHLER: Mustang laesst sich nicht ausfuehren." >&2
    exit 1
fi
log "Mustang ist einsatzbereit."

cat > "${TOOLS_DIR}/versions.env" <<EOF
# Automatisch erzeugt von build/fetch-validators.sh – nicht von Hand aendern.
MUSTANG_VERSION=${MUSTANG_VERSION}
MUSTANG_JAR=${MUSTANG_JAR}
EOF

echo "Fertig. Versionen in tools/versions.env."
