import { test, expect } from "@playwright/test";

test("propagates stop output state", async ({ page }) => {
  test.fail(true, "US2 emulator automation wiring pending");

  await page.goto("http://127.0.0.1:7777/output/camera-1");
  await page.getByRole("button", { name: "Stop Output" }).click();
  await expect(page.getByText("STOPPED")).toBeVisible();
});
