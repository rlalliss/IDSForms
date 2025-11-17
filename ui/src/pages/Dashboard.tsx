import * as React from "react";
import { JSX } from "react";
import { Link, useNavigate } from "react-router-dom";
import AppShell from "../components/AppShell";

type Stat = {
  label: string;
  value: number | string;
  sub?: string;
};

type Activity = {
  id: string;
  form: string;
  user: string;
  status: "Emailed" | "Signed" | "Ready" | "Error";
  ts: string; // e.g., "09:18"
};

function StatCard({ label, value, sub }: Stat) {
  return (
    <div className="stat-card">
      <div className="stat-card__label">{label}</div>
      <div className="stat-card__value">{value}</div>
      {sub ? <div className="stat-card__sub">{sub}</div> : null}
    </div>
  );
}

function Badge({ status }: { status: Activity["status"] }) {
  const tone =
    status === "Emailed"
      ? "badge--blue"
      : status === "Signed"
      ? "badge--green"
      : status === "Ready"
      ? "badge--gray"
      : "badge--red";
  return <span className={`badge ${tone}`}>{status}</span>;
}

export default function Dashboard(): JSX.Element {
  const navigate = useNavigate();
  // Placeholder data — replace with real API calls
  const [stats] = React.useState<Stat[]>([
    { label: "Forms Available", value: 30 },
    { label: "Submissions Today", value: 7, sub: "since midnight" },
    { label: "Pending Emails", value: 2, sub: "queued to send" },
  ]);

  const [recent] = React.useState<Activity[]>([
    { id: "SUB-1043", form: "Test Drive Agreement", user: "Rick L", status: "Emailed", ts: "09:18" },
    { id: "SUB-1042", form: "Credit App", user: "Chris P", status: "Signed", ts: "08:54" },
    { id: "SUB-1041", form: "Loan Payoff Auth", user: "Ava T", status: "Ready", ts: "08:31" },
  ]);

  const today = React.useMemo(
    () =>
      new Date().toLocaleDateString(undefined, {
        weekday: "short",
        month: "short",
        day: "numeric",
      }),
    []
  );

  const actions = (
    <>
      <button className="btn btn--primary" onClick={() => navigate("/fill")}>
        New Submission
      </button>
      <button className="btn btn--outline" onClick={() => navigate("/forms")}>
        Forms Catalog
      </button>
      <button className="btn btn--ghost" onClick={() => navigate("/settings")}>
        Manage Defaults
      </button>
    </>
  );

  return (
    <AppShell
      title="Dashboard"
      subtitle={`Operations snapshot for ${today}`}
      actions={actions}
      breadcrumbs={[{ label: "Workspace" }, { label: "Dashboard" }]}
    >
      <div className="stats-grid">
        {stats.map((s) => (
          <StatCard key={s.label} {...s} />
        ))}
      </div>

      <div className="panel-grid">
        <section className="panel">
          <div className="panel-header">
            <div>
              <p className="stat-card__label" style={{ letterSpacing: "0.25em" }}>
                Recent
              </p>
              <h2>Activity</h2>
            </div>
            <Link className="shortcut-link" to="/submissions">
              <span className="shortcut-dot" />
              View history
            </Link>
          </div>
          <div className="table-wrapper">
            <table className="table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Form</th>
                  <th>User</th>
                  <th>Status</th>
                  <th>Time</th>
                </tr>
              </thead>
              <tbody>
                {recent.map((r) => (
                  <tr key={r.id}>
                    <td style={{ fontWeight: 600 }}>{r.id}</td>
                    <td>{r.form}</td>
                    <td>{r.user}</td>
                    <td>
                      <Badge status={r.status} />
                    </td>
                    <td>{r.ts}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <aside className="panel">
          <h2>Shortcuts</h2>
          <ul className="shortcut-list">
            <li>
              <Link className="shortcut-link" to="/forms">
                <span className="shortcut-dot" />
                Fill a form
              </Link>
            </li>
            <li>
              <Link className="shortcut-link" to="/fill">
                <span className="shortcut-dot" />
                Start new submission
              </Link>
            </li>
            <li>
              <Link className="shortcut-link" to="/submissions">
                <span className="shortcut-dot" />
                Submission history
              </Link>
            </li>
            <li>
              <Link className="shortcut-link" to="/settings">
                <span className="shortcut-dot" />
                User defaults
              </Link>
            </li>
            <li>
              <Link className="shortcut-link" to="/sign">
                <span className="shortcut-dot" />
                Sign a form
              </Link>
            </li>
          </ul>
          <div className="note-card">
            Tip: enable auto-formatting in your editor for consistently clean pull requests.
          </div>
        </aside>
      </div>
    </AppShell>
  );
}
