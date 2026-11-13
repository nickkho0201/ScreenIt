# CONSTRUCT assets

`logo.png` in the repository root is the owner's supplied visual reference.
`screenit.svg` is its orthogonal production interpretation, with outlined `S_IT`
lettering and the fixed CONSTRUCT colors. `generate_icon.py` generates this SVG
and the embedded multi-size `ScreenIt.ico` using only Python's standard library.
The 16/20 px micro masters omit alignment ticks and simplify lettering; larger
masters retain the full frame. Icon generation does not install fonts.

Commissioner Regular/SemiBold/Bold and Unbounded Bold are bundled as WPF resources
under `src/ScreenIt.App/Assets/Fonts/`. These static TTFs and their original
`OFL.txt` files came from the owner's local Google Fonts family archives.
Upstreams: [Commissioner](https://github.com/kosbarts/Commissioner),
[Unbounded](https://github.com/google/fonts/tree/main/ofl/unbounded).
Both are distributed under the included SIL Open Font License; the license
files are copied to build and portable output. No system font installation or
runtime download is required. Verification checks the actual app-resource font
URIs and Cyrillic/Latin glyph coverage.
