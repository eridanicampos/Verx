#!/usr/bin/env python3
"""
Governança de licenças: verifica TODAS as dependências NuGet (diretas e transitivas,
a partir dos packages.lock.json) contra uma allowlist de licenças permissivas para uso comercial.

Uso: python3 scripts/check-licenses.py [--report docs/governanca/inventario-licencas.md]
Saída != 0 se algum pacote tiver licença fora da allowlist ou desconhecida.
"""
import glob
import json
import os
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET
from concurrent.futures import ThreadPoolExecutor

ALLOWED = {"MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "PostgreSQL", "ISC", "MS-PL", "0BSD", "Unlicense", "Zlib"}
# MPL-2.0 só é aceita em licença dupla com Apache-2.0 (caso do RabbitMQ.Client): optamos pela Apache-2.0.
# Pacotes antigos publicados apenas com licenseUrl (sem expressão SPDX), verificados manualmente.
KNOWN_URLS = {
    "https://licenses.nuget.org/MIT": "MIT",
    "https://github.com/dotnet/corefx/blob/master/LICENSE.TXT": "MIT",
    "https://dotnet.microsoft.com/en/dotnet_library_license.htm": "MIT",
}


def packages():
    found = set()
    for lock in glob.glob("**/packages.lock.json", recursive=True):
        if "/bin/" in lock or "/obj/" in lock:
            continue
        with open(lock, encoding="utf-8") as f:
            data = json.load(f)
        for deps in data.get("dependencies", {}).values():
            for name, info in deps.items():
                if info.get("type") == "Project":
                    continue
                found.add((name, info["resolved"]))
    return sorted(found)


def license_of(name, version):
    url = f"https://api.nuget.org/v3-flatcontainer/{name.lower()}/{version.lower()}/{name.lower()}.nuspec"
    with urllib.request.urlopen(url, timeout=30) as r:
        root = ET.fromstring(r.read())
    ns = {"n": root.tag.split("}")[0].strip("{")}
    lic = root.find(".//n:metadata/n:license", ns)
    if lic is not None and lic.get("type") == "expression":
        return lic.text.strip()
    lic_url = root.find(".//n:metadata/n:licenseUrl", ns)
    if lic_url is not None and lic_url.text:
        return KNOWN_URLS.get(lic_url.text.strip(), "URL:" + lic_url.text.strip())
    return "UNKNOWN"


def allowed(expr):
    if expr in ALLOWED:
        return True
    # Expressão "A OR B": basta uma das opções ser permitida (escolhemos a permissiva).
    options = [p.strip("() ") for p in re.split(r"\s+OR\s+", expr)]
    return len(options) > 1 and any(o in ALLOWED for o in options)


def main():
    report = sys.argv[sys.argv.index("--report") + 1] if "--report" in sys.argv else None
    pkgs = packages()
    with ThreadPoolExecutor(16) as pool:
        lics = list(pool.map(lambda p: license_of(*p), pkgs))

    rows = [(name, version, lic, allowed(lic)) for (name, version), lic in zip(pkgs, lics)]
    summary = {}
    for _, _, lic, _ in rows:
        summary[lic] = summary.get(lic, 0) + 1
    print(f"{len(rows)} pacotes analisados: " + ", ".join(f"{k}={v}" for k, v in sorted(summary.items())))

    if report:
        report_dir = os.path.dirname(report)
        if report_dir:  # caminho sem diretório (ex.: "license-report.md") -> diretório atual
            os.makedirs(report_dir, exist_ok=True)
        with open(report, "w", encoding="utf-8") as f:
            f.write("# Inventário de licenças\n\n")
            f.write("> Gerado automaticamente por `scripts/check-licenses.py` a partir dos `packages.lock.json` "
                    "(dependências diretas e transitivas). Executado no CI a cada push.\n\n")
            f.write(f"Total de pacotes: **{len(rows)}**\n\n| Licença | Pacotes |\n|---|---|\n")
            for k, v in sorted(summary.items()):
                f.write(f"| {k} | {v} |\n")
            f.write("\n| Pacote | Versão | Licença | Permitida |\n|---|---|---|---|\n")
            for name, version, lic, ok in rows:
                f.write(f"| {name} | {version} | {lic} | {'sim' if ok else 'NÃO'} |\n")

    bad = [r for r in rows if not r[3]]
    for name, version, lic, _ in bad:
        print(f"::error::Licença não permitida: {name} {version} -> {lic}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
