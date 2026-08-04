#!/usr/bin/env bash
#
# Gegenpruefung der erzeugten Golden-Master-Dateien mit dem offiziellen
# CEN-Schematron und veraPDF, beide ueber die Mustang-CLI.
#
# Das ist der Nachweis, dass die eigene Regelimplementierung mit der Norm
# uebereinstimmt (docs/DECISIONS.md, ADR-0002 und ADR-0004). Ohne diesen Schritt
# waere die Aussage "EN-16931-konform" nur eine Behauptung.
#
# Erwartete Verzeichnisstruktur (wird von den Tests befuellt):
#   artifacts/golden-masters/valid/     – muss fehlerfrei validieren
#   artifacts/golden-masters/invalid/   – muss beanstandet werden
#
# Rueckgabewert 0 nur, wenn alle Erwartungen erfuellt sind.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
GOLDEN_DIR="${ROOT_DIR}/artifacts/golden-masters"
VERSIONS_FILE="${ROOT_DIR}/tools/versions.env"

if [[ ! -f "${VERSIONS_FILE}" ]]; then
    echo "FEHLER: tools/versions.env fehlt. Zuerst build/fetch-validators.sh ausfuehren." >&2
    exit 1
fi

# shellcheck source=/dev/null
source "${VERSIONS_FILE}"

if [[ ! -f "${MUSTANG_JAR}" ]]; then
    echo "FEHLER: ${MUSTANG_JAR} nicht gefunden." >&2
    exit 1
fi

if [[ ! -d "${GOLDEN_DIR}" ]]; then
    echo "FEHLER: ${GOLDEN_DIR} fehlt." >&2
    echo "Die Golden Master entstehen beim Testlauf. Zuerst ausfuehren:" >&2
    echo "  dotnet test EInvoiceSender.slnx -c Release" >&2
    exit 1
fi

failures=0
checked=0

# Prueft eine Datei und vergleicht das Ergebnis mit der Erwartung.
# $1 = Pfad, $2 = "valid" oder "invalid"
check_file() {
    local file="$1" expectation="$2" output exit_code

    output="$(java -jar "${MUSTANG_JAR}" --action validate --no-notices \
        --source "${file}" 2>&1)"
    exit_code=$?
    checked=$((checked + 1))

    local summary
    summary="$(printf '%s' "${output}" | grep -oE '<summary status="[a-z]+"' | head -n1)"

    if [[ "${expectation}" == "valid" ]]; then
        if [[ ${exit_code} -eq 0 ]]; then
            printf '  [ok]   %s\n' "$(basename "${file}")"
        else
            printf '  [FEHL] %s – erwartet gueltig, Validator meldet Fehler\n' "$(basename "${file}")"
            printf '%s\n' "${output}" | grep -E '<error|<message|criterion' | head -n 15 | sed 's/^/         /'
            failures=$((failures + 1))
        fi
    else
        if [[ ${exit_code} -ne 0 ]]; then
            printf '  [ok]   %s – wie erwartet beanstandet\n' "$(basename "${file}")"
        else
            printf '  [FEHL] %s – erwartet ungueltig, Validator meldet aber Erfolg %s\n' \
                "$(basename "${file}")" "${summary}"
            failures=$((failures + 1))
        fi
    fi
}

for expectation in valid invalid; do
    dir="${GOLDEN_DIR}/${expectation}"
    [[ -d "${dir}" ]] || continue

    echo "Pruefe ${expectation}:"
    while IFS= read -r -d '' file; do
        check_file "${file}" "${expectation}"
    done < <(find "${dir}" -type f \( -name '*.xml' -o -name '*.pdf' \) -print0 | sort -z)
done

echo
echo "Geprueft: ${checked}, Abweichungen: ${failures}"

if [[ ${checked} -eq 0 ]]; then
    echo "FEHLER: Keine Golden Master gefunden – die Gegenpruefung waere sonst wertlos." >&2
    exit 1
fi

if [[ ${failures} -gt 0 ]]; then
    exit 1
fi

echo "Alle Golden Master entsprechen der Erwartung."
