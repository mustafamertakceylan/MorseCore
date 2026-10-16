# Wiki maintenance / Wiki bakımı

This folder is the canonical source for the public [MorseCore Wiki](https://github.com/mustafamertakceylan/MorseCore/wiki).
Edit these pages instead of maintaining a second copy in the Wiki editor.

Bu klasör Wiki'nin tek kaynağıdır. İki ayrı metin kopyası tutmak yerine burada
düzenleme yap. Sayfalar Türkçe ve İngilizce eşleriyle `pages.json` içinde tanımlıdır.

## Validate and render

```sh
python scripts/build-wiki.py --check
python scripts/build-wiki.py --output build/wiki
```

The script checks local document/image links and language pairs, converts links
to Wiki or repository URLs, and generates `_Sidebar.md` and `_Footer.md`.
`{{VERSION}}` and download URLs come from `CMakeLists.txt`; no version is maintained
separately in the pages. Images use the repository's `docs/media/` files.

Generated Markdown is not committed to the main repository. CI validates the
source whenever this folder, the script, media or the version changes.

## Publish

GitHub must have an initial Wiki page before its Git repository can be cloned.
Once initialized, use your existing GitHub account credentials:

```sh
git clone https://github.com/mustafamertakceylan/MorseCore.wiki.git cmake-build-agent-work/wiki-publish
python scripts/build-wiki.py --output cmake-build-agent-work/wiki-publish
git -C cmake-build-agent-work/wiki-publish diff --check
```

Review the generated files, stage only those names, commit and push the Wiki
repository. Do not remove unrelated pages or force-push. If the checkout already
exists, check that it is clean and pull with `--ff-only` before rendering.

Push the canonical source and image files to the main repository before pushing
the Wiki so that public links resolve. The Wiki has its own commit history; this
operation does not create an application release or move a version tag.

No new token is created and no GitHub Actions secret is required by the renderer.
CI validates/renders only; publishing uses the maintainer's Git credentials.
