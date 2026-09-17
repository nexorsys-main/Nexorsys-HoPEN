import { readCsrfToken } from "./csrf";

describe("CSRF session protection", () => {
  it("reads only the NexorSys CSRF cookie", () => {
    expect(readCsrfToken("other=x; nexorsys_csrf=abc123; theme=dark")).toBe("abc123");
    expect(readCsrfToken("other=x")).toBeNull();
  });
});
