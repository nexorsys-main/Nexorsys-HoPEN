import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import DashboardWorkflowChart from "./DashboardWorkflowChart";

afterEach(cleanup);

describe("DashboardWorkflowChart", () => {
  it("renders counts received from the workflow API accessors", () => {
    render(
      <DashboardWorkflowChart counts={{ pending: 3, approved: 8, rejected: 2 }} />,
    );

    expect(screen.getByText("En attente")).toBeTruthy();
    expect(screen.getByText("Approuvés")).toBeTruthy();
    expect(screen.getByText("Rejetés")).toBeTruthy();
    expect(screen.getByText("8")).toBeTruthy();
    expect(screen.getByRole("progressbar", { name: "Approuvés" }).getAttribute("aria-valuenow")).toBe("8");
  });

  it("renders zero values accessibly when the organization has no workflows", () => {
    render(
      <DashboardWorkflowChart counts={{ pending: 0, approved: 0, rejected: 0 }} />,
    );

    expect(screen.getAllByText("0")).toHaveLength(3);
    expect(screen.getAllByRole("progressbar")).toHaveLength(3);
  });
});
