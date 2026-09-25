"""Decode a published QR PNG with the Android scanner's ZXing version and compare launchUrl bytes."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

ZXING_VERSION = "3.5.3"


def cached_jar(project: Path, group: str, module: str, version: str) -> Path:
    root = project / ".gradle-user-home/caches/modules-2/files-2.1" / group / module / version
    matches = list(root.glob(f"*/{module}-{version}.jar"))
    if len(matches) != 1:
        raise RuntimeError(f"Expected one cached {module} {version} JAR; build Android once first: {root}")
    return matches[0]


def main() -> None:
    project = Path(__file__).resolve().parent.parent
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("--image", type=Path, required=True)
    args.add_argument("--publish", type=Path, required=True, help="Publisher response JSON containing launchUrl")
    args.add_argument("--output", type=Path, default=project / "Artifacts/qr-decode-verification.json")
    args.add_argument("--java", type=Path, default=Path(
        r"C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\java.exe"))
    args.add_argument("--verify-world-link", action="store_true", help="Also run the compiled Android WorldLink parser")
    args.add_argument("--allow-http", action="store_true", help="Use the development parser policy")
    args.add_argument("--try-harder", action="store_true", help="Use ZXing's exhaustive finder-pattern scan")
    options = args.parse_args()
    image = options.image.resolve()
    published = json.loads(options.publish.read_text(encoding="utf-8-sig"))
    expected = published["launchUrl"]
    if not isinstance(expected, str) or not expected:
        raise RuntimeError("publish.launchUrl must be a nonempty string")
    classpath = [cached_jar(project, "com.google.zxing", "core", ZXING_VERSION)]
    if options.verify_world_link:
        classes = project / "app/build/tmp/kotlin-classes/debug"
        if not (classes / "com/kimchily/app/WorldLink.class").is_file():
            raise RuntimeError("Compile the Android debug app before verifying its WorldLink parser")
        classpath += [classes, cached_jar(project, "org.jetbrains.kotlin", "kotlin-stdlib", "1.6.21")]
    # Compile explicitly: the bundled Java 11 source-file launcher can misparse
    # application arguments on Windows (Arrays.copyOfRange in launcher.Main).
    decoder_classes = project / "Artifacts/qr-decoder-classes"
    decoder_classes.mkdir(parents=True, exist_ok=True)
    compile_result = subprocess.run([
        str(options.java.with_name("javac.exe" if os.name == "nt" else "javac")),
        "-encoding", "UTF-8", "--class-path", os.pathsep.join(map(str, classpath)),
        "-d", str(decoder_classes), str(project / "tools/DecodeQr.java"),
    ], capture_output=True, encoding="utf-8")
    if compile_result.returncode:
        raise RuntimeError("QR decoder compilation failed: " + compile_result.stderr.strip())
    classpath.append(decoder_classes)
    command = [str(options.java), "-Djava.awt.headless=true", "--class-path", os.pathsep.join(map(str, classpath)),
               "DecodeQr", str(image)]
    if options.verify_world_link:
        command.append("--verify-world-link")
        if options.allow_http:
            command.append("--allow-http")
    if options.try_harder:
        command.append("--try-harder")
    process = subprocess.run(command, capture_output=True, encoding="utf-8")
    if process.returncode:
        raise RuntimeError("QR decoder failed: " + process.stderr.strip())
    decoded = process.stdout
    report = {
        "image": str(image), "imageSha256": hashlib.sha256(image.read_bytes()).hexdigest(),
        "publishResponse": str(options.publish.resolve()), "decoder": f"ZXing core {ZXING_VERSION} / Java ImageIO",
        "matchesLaunchUrl": decoded == expected, "decodedText": decoded,
        "worldLinkParserChecked": options.verify_world_link,
        "tryHarder": options.try_harder,
    }
    if options.verify_world_link:
        evidence = [line for line in process.stderr.splitlines() if line.startswith("WORLD_LINK_OK\t")]
        if len(evidence) != 1:
            raise RuntimeError("Missing WorldLink parser result")
        _, world, revision, url, checksum = evidence[0].split("\t")
        report["parsedTarget"] = {"worldId": world, "revisionId": revision, "manifestUrl": url, "manifestSha256": checksum}
        for field, actual in report["parsedTarget"].items():
            if published.get(field) != actual:
                raise RuntimeError(f"Decoded WorldLink {field} differs from publisher response")
    options.output.parent.mkdir(parents=True, exist_ok=True)
    options.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if decoded != expected:
        raise RuntimeError(f"QR text differs from launchUrl; inspect {options.output}")
    print(f"PASS: actual QR decode exactly matches launchUrl ({len(decoded)} characters)")
    if options.verify_world_link:
        print("PASS: compiled WorldLink parser fields match publisher response")
    print(f"Report: {options.output.resolve()}")


if __name__ == "__main__":
    main()
