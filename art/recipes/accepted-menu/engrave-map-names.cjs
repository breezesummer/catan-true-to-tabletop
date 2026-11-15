// 从随包宋体的笔画遮罩生成刻槽。输出每设计单位四个像素，透明槽底透出实际木纹。
const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('sharp');

const resources = path.resolve(__dirname, '../../../Unity/Catan/Assets/Resources');
const menu = path.join(resources, 'AcceptedMenu');
const scale = 4;

async function engrave(name) {
    const { data, info } = await sharp({ text: {
        text: name,
        font: 'Noto Serif CJK SC 100',
        fontfile: path.join(resources, 'Fonts/NotoSerifCJKsc-Regular.otf'),
        dpi: 72,
        rgba: true
    }}).extend({ top: 8, bottom: 8, left: 8, right: 8, background: '#0000' })
        .raw().toBuffer({ resolveWithObject: true });
    const { width, height } = info;
    const result = Buffer.alloc(width * height * 4);
    function coverage(x, y) {
        if (x < 0 || y < 0 || x >= width || y >= height) return 0;
        return data[(y * width + x) * 4 + 3] / 255;
    }
    for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
            const cut = coverage(x, y);
            const upperWall = cut * (1 - coverage(x - 2, y - scale));
            const lowerWall = cut * (1 - coverage(x + 2, y + scale));
            const lip = (1 - cut) * coverage(x - 2, y - scale);
            const wallAlpha = cut * .91;
            const lipAlpha = lip * .78;
            const alpha = wallAlpha + lipAlpha * (1 - wallAlpha);
            if (!alpha) continue;
            const floor = [99, 54, 24], highlight = [255, 223, 158];
            const index = (y * width + x) * 4;
            for (let c = 0; c < 3; c++) {
                const wall = floor[c] * (1 - .65 * upperWall) + [36, 23, 13][c] * lowerWall;
                result[index + c] = Math.round((wall * wallAlpha + highlight[c] * lipAlpha * (1 - wallAlpha)) / alpha);
            }
            result[index + 3] = Math.round(alpha * 255);
        }
    }
    await sharp(result, { raw: { width, height, channels: 4 } }).png()
        .toFile(path.join(menu, 'setup-engraved-' + name + '.png'));
}

(async () => {
    for (const file of await fs.readdir(menu)) {
        if (file.startsWith('map-') && file.endsWith('.png')) await engrave(file.slice(4, -4));
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
