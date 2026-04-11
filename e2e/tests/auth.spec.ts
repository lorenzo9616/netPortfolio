import { test, expect } from '@playwright/test';

const BASE = 'http://localhost:3000';

test.describe('Authentication', () => {
  test('redirects unauthenticated user to /login', async ({ page }) => {
    await page.goto(`${BASE}/properties`);
    await expect(page).toHaveURL(/\/login/);
  });

  test('shows login form with username and password fields', async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await expect(page.getByLabel(/username/i)).toBeVisible();
    await expect(page.getByLabel(/password/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();
  });

  test('shows error on bad credentials', async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.getByLabel(/username/i).fill('admin');
    await page.getByLabel(/password/i).fill('wrongpassword');
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByRole('alert')).toContainText(/incorrect username or password/i);
  });

  test('successful login redirects to /properties', async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.getByLabel(/username/i).fill('admin');
    await page.getByLabel(/password/i).fill('openocr2024');
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page).toHaveURL(/\/properties/);
  });

  test('sign out clears session and returns to /login', async ({ page }) => {
    // Login first
    await page.goto(`${BASE}/login`);
    await page.getByLabel(/username/i).fill('admin');
    await page.getByLabel(/password/i).fill('openocr2024');
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page).toHaveURL(/\/properties/);
    // Sign out
    await page.getByRole('button', { name: /sign out/i }).click();
    await expect(page).toHaveURL(/\/login/);
  });
});
