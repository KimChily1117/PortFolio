# Third-party dependencies

Dependencies are installed into the ignored `.deps` directory; their original license files remain in the installed distributions.

- **qrcode 8.2** — BSD-3-Clause license, Lincoln Loop and contributors. Used to encode the actual Kimchily app launch URI into a QR symbol. Project: https://github.com/lincolnloop/python-qrcode
- **Pillow 12.3.0** — MIT-CMU license / historical PIL license, the Pillow contributors and Secret Labs AB/Fredrik Lundh. Used to serialize QR raster images as PNG. Project: https://github.com/python-pillow/Pillow

Keep the distributions' complete license notices when redistributing bundled dependencies. The service's storage and ZIP validation use Python standard-library modules.
