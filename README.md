# Nasreddins-Magic-Toolbox

A small helper application to do some simple magic tricks with a smartphone.

## Tarot deck assets

The Rider-Waite tarot deck images are no longer stored directly in the Git
repository to avoid binary files in pull requests.  Run the following command to
download them into `Images/TarotDeck_Wikipedia/` when you set up the project:

```bash
python scripts/download_tarot_deck.py
```

The downloader fetches the public domain artwork from Wikimedia Commons.  See
`Images/TarotDeck_Wikipedia/README.md` for more information.
