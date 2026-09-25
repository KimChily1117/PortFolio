"""Regenerate the Android QR test images from their Kotlin test contract, without publishing a world."""
import json
from pathlib import Path
import re
import sys
from urllib.parse import parse_qs, urlsplit


def main() -> None:
    project = Path(__file__).resolve().parent.parent
    # Share the publisher's pinned qrcode dependency when it is installed locally.
    local_dependencies = project.parent / "KimchilyPublish/.deps"
    if local_dependencies.is_dir():
        sys.path.insert(0, str(local_dependencies))
    import qrcode

    contract = (project / "app/src/test/java/com/kimchily/app/QrDecoderTest.kt").read_text(encoding="utf-8")
    prefix = re.search(r'val expected = "([^"]+)"', contract).group(1)
    # The third fixture is a real server response and is deliberately not regenerated.
    fixtures = re.findall(r'Fixture\("(/qr-published-v[12]\.png)",\s*"([^"]+)",\s*"([0-9a-f]{64})"\)', contract)
    if len(fixtures) != 2 or not prefix.startswith("kimchily://world?manifest="):
        raise RuntimeError("Unexpected QR fixture contract; review before regenerating images")
    report_directory = project / "Artifacts/rebrand-qr-fixtures"
    report_directory.mkdir(parents=True, exist_ok=True)
    for filename, revision, checksum in fixtures:
        link = prefix + revision + "%2Fworld.json&sha256=" + checksum
        # Match KimchilyPublish/server.py. Automatic masks triggered false finder
        # candidates in the camera detector for otherwise valid published links.
        qr = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M,
                           box_size=8, border=4, mask_pattern=0)
        qr.add_data(link)
        qr.make(fit=True)
        destination = project / "app/src/test/resources" / filename.lstrip("/")
        qr.make_image(fill_color="black", back_color="white").save(destination)
        manifest = parse_qs(urlsplit(link).query)["manifest"][0]
        details = {
            "fixtureOnly": True,
            "worldId": urlsplit(manifest).path.split("/")[2],
            "revisionId": revision,
            "manifestUrl": manifest,
            "manifestSha256": checksum,
            "launchUrl": link,
        }
        (report_directory / (destination.stem + ".json")).write_text(
            json.dumps(details, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Generated {destination.name}: {len(link)} characters; test-only URI")


if __name__ == "__main__":
    main()
