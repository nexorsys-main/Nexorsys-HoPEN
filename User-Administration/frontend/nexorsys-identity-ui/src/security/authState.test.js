import { authStateFromFailure, requestFailureMessage } from "./authState";

describe("authentication availability state", () => {
  it.each([
    [{ response: { status: 401 } }, "unauthorized"],
    [{ response: { status: 403 } }, "forbidden"],
    [{ response: { status: 500 } }, "error"],
    [{ message: "network error" }, "unavailable"],
    [{ response: { status: 401 }, expiredSession: true }, "unauthorized"],
    [{ response: { status: 503 } }, "error"],
  ])("classifies failure without converting it to a healthy state", (error, expected) => {
    expect(authStateFromFailure(error)).toBe(expected);
  });
});

describe("request failure messaging", () => {
  it.each([
    [{ response: { status: 401 } }, "Session expirée"],
    [{ response: { status: 403 } }, "Accès refusé"],
    [{ response: { status: 503 } }, "temporairement indisponible"],
    [{ message: "network error" }, "Impossible de joindre"],
  ])("keeps API failures visible to the operator", (error, expected) => {
    expect(requestFailureMessage(error, "service")).toContain(expected);
  });
});
