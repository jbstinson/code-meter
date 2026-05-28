import { chromium } from 'playwright';
import { mkdirSync } from 'fs';

mkdirSync(new URL('../', import.meta.url).pathname.replace(/^\//, ''), { recursive: true });

const pages = [
  { name: 'popup',       url: 'http://localhost:7432/popup.html',       width: 400,  height: 300 },
  { name: 'settings',    url: 'http://localhost:7432/settings.html',    width: 420,  height: 480 },
  { name: 'tray-states', url: 'http://localhost:7432/tray-states.html', width: 420,  height: 200 },
  { name: 'toast',       url: 'http://localhost:7432/toast.html',       width: 420,  height: 160 },
];

const browser = await chromium.launch();

for (const { name, url, width, height } of pages) {
  const page = await browser.newPage();
  await page.setViewportSize({ width, height });
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForTimeout(300);
  await page.screenshot({
    path: `docs/screenshots/${name}.png`,
    fullPage: false,
  });
  console.log(`✓ ${name}.png`);
  await page.close();
}

await browser.close();
console.log('Done.');
