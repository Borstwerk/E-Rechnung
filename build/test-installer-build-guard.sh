#!/usr/bin/env bash
#
# Beweist, dass sich das WiX-Installerprojekt nicht direkt bauen lässt.
#
# Warum es diesen Test gibt: Am 25.08.2026 entstand aus einem direkten Build
# des Installerprojekts ein MSI mit der Produktversion 0.2.0, dessen
# Programmbestand aber aus einem älteren Quellstand stammte. Der alte Publish
# lag noch in artifacts/publish/win-x64 und wurde stillschweigend paketiert.
# Versionsprüfungen konnten das nicht erkennen, weil auch der alte Stand
# bereits VersionPrefix 0.2.0 trug.
#
# Der Test prüft deshalb nicht, ob irgendwo ein String im Projekt steht,
# sondern startet einen echten Buildversuch und sieht nach, was passiert.
#
# Bewusst plattformneutral: Der Wächter ist reine MSBuild-Logik und greift,
# bevor die WiX-Werkzeugkette überhaupt anläuft. Damit ist er unter Linux
# genauso prüfbar wie unter Windows – und läuft in beiden CI-Jobs.
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root/installer/EInvoiceSender.Setup/EInvoiceSender.Setup.wixproj"
msi="$root/installer/EInvoiceSender.Setup/bin/Release/BorstWerk-E-Rechnung-Setup.msi"

work="$(mktemp -d)"
stale="$work/publish"
mkdir -p "$stale"

# Eine vermeintliche Anwendung im Zielordner. Sie soll die Sperre gerade
# NICHT umgehen: Der gefährliche Fall ist ja genau der, in dem eine Datei
# dieses Namens vorhanden, aber veraltet ist.
printf 'veralteter Programmbestand' > "$stale/EInvoiceSender.exe"

failures=0
passed=0

cleanup() { rm -rf "$work"; }
trap cleanup EXIT

fail() {
    echo "[FEHLGESCHLAGEN] $1"
    failures=$((failures + 1))
}

pass() {
    echo "[BESTANDEN] $1"
    passed=$((passed + 1))
}

# ---------------------------------------------------------------------------
# 1. Direkter Build ohne Autorisierung
# ---------------------------------------------------------------------------
name='Direkter Build des Installerprojekts wird abgewiesen'
rm -f "$msi"
out="$(cd "$root" && dotnet build "$project" -c Release \
    -p:PublishDir="$stale/" 2>&1)"
code=$?

if [ "$code" -eq 0 ]; then
    fail "$name: Der Build war erfolgreich (Exitcode 0), obwohl er scheitern muss."
elif ! grep -q 'Build-Installer.ps1' <<< "$out"; then
    fail "$name: Die Meldung verweist nicht auf Build-Installer.ps1."
    echo "$out" | tail -20
elif ! grep -q 'Build-Release.ps1' <<< "$out"; then
    fail "$name: Die Meldung verweist nicht auf Build-Release.ps1."
    echo "$out" | tail -20
elif [ -f "$msi" ]; then
    fail "$name: Es wurde trotzdem ein MSI erzeugt: $msi"
else
    pass "$name (Exitcode $code)"
fi

# Der Wächter muss früh greifen. Läuft WiX bereits an, hat der Build schon
# Arbeit geleistet und wir hätten uns nur zufällig an einer späteren Prüfung
# gefangen – etwa am ProductCode oder an einer fehlenden Datei.
name='Der Wächter greift, bevor die WiX-Werkzeugkette anläuft'
if grep -qi 'wix.exe' <<< "$out"; then
    fail "$name: wix.exe wurde bereits aufgerufen."
    echo "$out" | tail -20
else
    pass "$name"
fi

# ---------------------------------------------------------------------------
# 2. Autorisierter Build ohne PublishDir
# ---------------------------------------------------------------------------
# Ein vorhandener Standardordner darf niemals allein dadurch zur
# Installerquelle werden, dass PublishDir fehlt.
name='Autorisierter Build ohne PublishDir bricht ab'
rm -f "$msi"
out2="$(cd "$root" && dotnet build "$project" -c Release \
    -p:BorstWerkInstallerBuild=true 2>&1)"
code2=$?

if [ "$code2" -eq 0 ]; then
    fail "$name: Der Build war erfolgreich (Exitcode 0), obwohl PublishDir fehlt."
elif ! grep -q 'PublishDir' <<< "$out2"; then
    fail "$name: Die Meldung nennt PublishDir nicht."
    echo "$out2" | tail -20
elif [ -f "$msi" ]; then
    fail "$name: Es wurde trotzdem ein MSI erzeugt: $msi"
else
    pass "$name (Exitcode $code2)"
fi

# ---------------------------------------------------------------------------
# 3. Der unterstützte Weg kommt durch den Wächter
# ---------------------------------------------------------------------------
# Ein zu breiter Wächter wäre genauso schädlich wie gar keiner: Er würde den
# offiziellen Releaseweg blockieren. Geprüft wird deshalb der Wächter selbst –
# ein vollständiger MSI-Bau gehört nicht in diesen Test und liefe unter Linux
# ohnehin nicht.
name='Der autorisierte Weg mit PublishDir kommt durch den Wächter'
out3="$(cd "$root" && dotnet msbuild "$project" \
    -t:EnsureAuthorizedInstallerBuild \
    -p:Configuration=Release \
    -p:BorstWerkInstallerBuild=true \
    -p:PublishDir="$stale/" 2>&1)"
code3=$?

if [ "$code3" -ne 0 ]; then
    fail "$name: Der Wächter blockiert den unterstützten Weg (Exitcode $code3)."
    echo "$out3" | tail -20
else
    pass "$name"
fi

echo
if [ "$failures" -ne 0 ]; then
    echo "$failures Prüfung(en) fehlgeschlagen, $passed bestanden."
    exit 1
fi

echo "$passed Prüfungen des Installer-Buildwächters erfolgreich."
