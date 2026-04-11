import { test, expect } from '@playwright/test';

async function loginAsAdmin(page: import('@playwright/test').Page) {
  await page.goto('http://localhost:3000/login');
  await page.getByLabel(/username/i).fill('admin');
  await page.getByLabel(/password/i).fill('openocr2024');
  await page.getByRole('button', { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/properties/);
}

test.describe('Document History page', () => {
  test('navigates to /history from sidebar', async ({ page }) => {
    await loginAsAdmin(page);
    await page.getByRole('link', { name: /history/i }).click();
    await expect(page).toHaveURL(/\/history/);
  });

  test('shows either document rows or empty state', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('http://localhost:3000/history');
    // Either the table header or the empty state message must be visible
    const hasTable = await page.locator('table').isVisible().catch(() => false);
    const hasEmpty = await page.getByText(/no documents analyzed yet/i).isVisible().catch(() => false);
    expect(hasTable || hasEmpty).toBe(true);
  });
});
