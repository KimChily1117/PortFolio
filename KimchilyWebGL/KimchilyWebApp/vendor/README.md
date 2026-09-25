# jsQR 1.4.0

`jsQR-1.4.0.js` is the unmodified browser distribution from the official `jsqr`
1.4.0 npm package, repository https://github.com/cozmo/jsQR, commit
`49a9633931fb8030ac2fc9cecc121d6e5a19f9a3`. Its Apache-2.0 license is preserved in
`jsQR-LICENSE.txt`. `jsqr-provenance.json` records the npm SHA-512 integrity and
deployed file SHA-256 hashes. `fetch-jsqr.ps1` reproducibly fetches, verifies and
extracts just the distribution, license and package metadata. No npm install or
package lifecycle script is run. There is no runtime CDN dependency.

The scanner feeds bounded canvas images to jsQR on camera frames or local image
files, so it does not depend on the browser-specific BarcodeDetector API. QR
images are decoded locally and never uploaded. A decoded string is not an
instruction to navigate: `qr.js` first checks its format and exact allowed origins,
then resolves it against the server's stored WebGL revision and checksum.
