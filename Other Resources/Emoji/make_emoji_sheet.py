# Builds the mod's emoji sheet from emoji-datasource-twitter: every emoji except flags and skin-tone variants,
# shrunk to 32px and packed into one image (one file loads and copies far faster than 1,600), plus a C# index of
# shortcodes in display order, which is also their order on the sheet.
import json, os, sys
from PIL import Image
src, tex_out, cs_out = sys.argv[1], sys.argv[2], sys.argv[3]
data = json.load(open(os.path.join(src, 'emoji.json')))
keep = [e for e in data if e['category'] not in ('Flags', 'Component') and e.get('has_img_twitter')
        and os.path.exists(os.path.join(src, 'img/twitter/64', e['image']))]
keep.sort(key=lambda e: e['sort_order'])
# Each emoji sits in a 40px cell with 4px of clear space around it, so neighbours don't bleed into each other when
# the game shrinks or compresses the sheet (40 is a multiple of 4, matching the compression's 4x4 blocks)
SIZE, CELL, COLUMNS = 32, 40, 41
rows = (len(keep) + COLUMNS - 1) // COLUMNS
sheet = Image.new('RGBA', (COLUMNS * CELL, rows * CELL), (0, 0, 0, 0))
for i, e in enumerate(keep):
    im = Image.open(os.path.join(src, 'img/twitter/64', e['image'])).convert('RGBA').resize((SIZE, SIZE), Image.LANCZOS)
    sheet.paste(im, (i % COLUMNS * CELL + (CELL - SIZE) // 2, i // COLUMNS * CELL + (CELL - SIZE) // 2))
sheet.save(tex_out, optimize=True)
cats = []
for e in keep:
    if e['category'] not in cats: cats.append(e['category'])
def q(s): return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'
lines = []
lines.append('// Generated from emoji-datasource-twitter (Twemoji art, CC-BY 4.0). Do not edit by hand.')
lines.append('namespace PhinixClient')
lines.append('{')
lines.append('    internal static class EmojiIndex')
lines.append('    {')
lines.append('        /// <summary>Picker categories, in order</summary>')
lines.append('        internal static readonly string[] Categories = { ' + ', '.join(q(c) for c in cats) + ' };')
lines.append('')
lines.append('        /// <summary>The sheet (Textures/Phinix/Emoji.png): emojis in the order of <see cref="All"/>, left to right then down</summary>')
lines.append('        internal const int SheetColumns = %d, SheetRows = %d, CellSize = %d, EmojiSize = %d;' % (COLUMNS, rows, CELL, SIZE))
lines.append('')
lines.append('        /// <summary>Each emoji: its main shortcode, other shortcodes, and its category index</summary>')
lines.append('        internal static readonly (string Name, string[] Aliases, int Category)[] All =')
lines.append('        {')
for e in keep:
    aliases = [n for n in e['short_names'] if n != e['short_name']]
    lines.append('            (' + q(e['short_name']) + ', new string[] { ' + ', '.join(q(a) for a in aliases) + ' }, ' + str(cats.index(e['category'])) + '),')
lines.append('        };')
lines.append('    }')
lines.append('}')
open(cs_out, 'w', encoding='utf-8-sig').write('\n'.join(lines) + '\n')
print(len(keep), cats)
