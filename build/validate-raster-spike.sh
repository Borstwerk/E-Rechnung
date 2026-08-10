#!/usr/bin/env bash
#
# Gegenprüfung der Dateien aus dem Rasterversuch (docs/SPIKE-RASTER-FALLBACK.md).
#
# Der Unterschied zu build/validate-golden-masters.sh: Hier genügt es nicht,
# dass keine Teilzusammenfassung "invalid" meldet. Verlangt wird zusätzlich die
# ausdrückliche Aussage von veraPDF, also 'isCompliant=true' im PDF-Abschnitt.
# Genau darum geht es in diesem Versuch – die Frage ist nicht, ob unsere eigene
# Prüfung zufrieden ist, sondern ob ein fremdes Werkzeug die Datei als PDF/A-3b
# anerkennt.
#
# Erwartetes Verzeichnis (wird vom Testlauf befüllt):
#   artifacts/spike-raster/   – jede Datei muss vollständig bestehen
#
# Rückgabewert 0 nur, wenn jede Datei besteht.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
SPIKE_DIR="${ROOT_DIR}/artifacts/spike-raster"
VERSIONS_FILE="${ROOT_DIR}/tools/versions.env"

if [[ ! -f "${VERSIONS_FILE}" ]]; then
    echo "FEHLER: tools/versions.env fehlt. Zuerst build/fetch-validators.sh ausführen." >&2
    exit 1
fi

# shellcheck source=/dev/null
source "${VERSIONS_FILE}"

if [[ ! -f "${MUSTANG_JAR}" ]]; then
    echo "FEHLER: ${MUSTANG_JAR} nicht gefunden." >&2
    exit 1
fi

if [[ ! -d "${SPIKE_DIR}" ]]; then
    echo "FEHLER: ${SPIKE_DIR} fehlt." >&2
    echo "Die Dateien entstehen beim Testlauf. Zuerst ausführen:" >&2
    echo "  dotnet test tests/EInvoiceSender.IntegrationTests -c Release" >&2
    exit 1
fi

failures=0
checked=0

printf '%-38s %9s %6s %10s %8s %8s\n' \
    "Datei" "Größe/KB" "Seiten" "veraPDF" "Regeln" "Ergebnis"
printf '%.0s-' {1..86}; printf '\n'

while IFS= read -r -d '' file; do
    output="$(java -jar "${MUSTANG_JAR}" --action validate --no-notices \
        --source "${file}" 2>&1)"
    exit_code=$?
    checked=$((checked + 1))

    invalid_sections="$(printf '%s' "${output}" | grep -c '<summary status="invalid"' || true)"

    # Die ausdrückliche Aussage von veraPDF, nicht nur die Zusammenfassung.
    compliant="nein"
    if printf '%s' "${output}" | grep -q 'isCompliant=true'; then
        compliant="ja"
    fi

    # Wie viele Schematron-Regeln haben angeschlagen, wie viele sind gescheitert?
    fired="$(printf '%s' "${output}" | grep -oE '<fired>[0-9]+' | grep -oE '[0-9]+' | head -n1)"
    failed_rules="$(printf '%s' "${output}" | grep -oE '<failed>[0-9]+' | grep -oE '[0-9]+' | head -n1)"

    size_kb=$(( ($(stat -c%s "${file}") + 1023) / 1024 ))
    pages="$(grep -ac '/Type\s*/Page[^s]' "${file}" 2>/dev/null || echo '?')"

    verdict="ok"
    if [[ ${exit_code} -ne 0 || ${invalid_sections} -gt 0 || "${compliant}" != "ja" ]]; then
        verdict="FEHL"
        failures=$((failures + 1))
    fi

    printf '%-38s %9s %6s %10s %4s/%-3s %8s\n' \
        "$(basename "${file}")" "${size_kb}" "${pages}" "${compliant}" \
        "${failed_rules:-?}" "${fired:-?}" "${verdict}"

    if [[ "${verdict}" == "FEHL" ]]; then
        printf '%s\n' "${output}" \
            | grep -oE 'errorMessage=[^],]*|clause=[^,]*' \
            | head -n 8 | sed 's/^/         /'
    fi
    # Nur die Ergebnisse, nicht die Vorlagen: Unter quellen/ liegen die
    # Eingangsdateien für den Sichtvergleich. Sie sind absichtlich nicht
    # normgerecht – das ist ja der Anlass des ganzen Versuchs.
done < <(find "${SPIKE_DIR}" -maxdepth 1 -type f -name '*.pdf' -print0 | sort -z)

echo
echo "Geprüft: ${checked}, Abweichungen: ${failures}"
echo "Spalte „Regeln“: gescheiterte/ausgelöste CEN-Schematron-Regeln."

if [[ ${checked} -eq 0 ]]; then
    echo "FEHLER: Keine Dateien gefunden – die Gegenprüfung wäre sonst wertlos." >&2
    exit 1
fi

if [[ ${failures} -gt 0 ]]; then
    exit 1
fi

echo "Jede Datei aus dem Rasterversuch wird von veraPDF als PDF/A-3b anerkannt."
