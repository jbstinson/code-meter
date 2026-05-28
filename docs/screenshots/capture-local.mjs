import { chromium } from 'playwright';
import { fileURLToPath } from 'url';
import { resolve, dirname } from 'path';

const __dir = dirname(fileURLToPath(import.meta.url));

const pages = [
  { name: 'popup',       file: 'src/popup.html',       width: 380,  height: 240 },
  { name: 'settings',    file: 'src/settings.html',    width: 440,  height: 500 },
  { name: 'tray-states', file: 'src/tray-states.html', width: 440,  height: 220 },
  { name: 'toast',       file: 'src/toast.html',       width: 420,  height: 160 },
];

const browser = await chromium.launch();

for (const { name, file, width, height } of pages) {
  const url = 'file:///' + resolve(__dir, file).replace(/\\/g, '/');
  const page = await browser.newPage();
  await page.setViewportSize({ width, height });
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForTimeout(400);
  await page.screenshot({
    path: resolve(__dir, `${name}.png`),
    fullPage: false,
  });
  console.log(`✓ ${name}.png`);
  await page.close();
}

await browser.close();
console.log('Done.');
