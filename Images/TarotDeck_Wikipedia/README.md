# Rider-Waite Tarot deck assets

This project originally stored every Rider-Waite card image directly in the
repository.  Because the platform that reviews this project does not accept
binary assets in pull requests, the images are now downloaded on demand.

Run the helper script to populate the folder with the public domain artwork from
Wikimedia Commons:

```bash
python scripts/download_tarot_deck.py
```

The script retrieves the images from the official Wikimedia Commons category
([Rider-Waite tarot deck](https://commons.wikimedia.org/wiki/Category:Rider-Waite_tarot_deck))
and saves them into this directory, matching the filenames that used to live in
the repository.

The original artwork is in the public domain.  See the Wikimedia Commons page
for licensing details and attribution information.
