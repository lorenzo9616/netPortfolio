import { test, expect } from '@playwright/test';

// Helper: log in before each test
async function loginAsAdmin(page: import('@playwright/test').Page) {
  await page.goto('http://localhost:3000/login');
  await page.getByLabel(/username/i).fill('admin');
  await page.getByLabel(/password/i).fill('openocr2024');
  await page.getByRole('button', { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/properties/);
}

test.describe('Upload page', () => {
  test('shows upload dropzone after login', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('http://localhost:3000/upload');
    // File input should be present
    await expect(page.locator('input[type="file"]')).toBeAttached();
  });
});
