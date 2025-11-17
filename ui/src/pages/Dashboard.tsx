import * as React from "react";
import { JSX } from "react";
import { Link, useNavigate } from "react-router-dom";

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
    <div className="rounded-2xl border border-gray-200 bg-white p-5 shadow-sm">
      <div className="text-sm text-gray-500">{label}</div>
      <div className="mt-1 text-3xl font-semibold tracking-tight">{value}</div>
      {sub ? <div className="mt-1 text-xs text-gray-400">{sub}</div> : null}
    </div>
  );
}

function Badge({ status }: { status: Activity["status"] }) {
  const cls =
    status === "Emailed"
      ? "bg-blue-50 text-blue-700"
      : status === "Signed"
      ? "bg-green-50 text-green-700"
      : status === "Ready"
      ? "bg-gray-100 text-gray-700"
      : "bg-red-50 text-red-700";
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs ${cls}`}>
      {status}
    </span>
  );
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

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Top bar */}
      <header className="border-b bg-white">
        <div className="mx-auto max-w-7xl px-6 py-4">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-xl font-semibold tracking-tight">Dashboard</h1>
              <p className="text-sm text-gray-500">{today}</p>
            </div>
            <div className="flex gap-2">
              <button
                className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-medium shadow-sm hover:bg-gray-50 active:translate-y-[1px]"
                onClick={() => navigate("/fill")}
              >
                New Submission
              </button>
              <button
                className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-medium shadow-sm hover:bg-gray-50 active:translate-y-[1px]"
                onClick={() => navigate("/forms")}
              >
                Open Forms Catalog
              </button>
              <button
                className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-medium shadow-sm hover:bg-gray-50 active:translate-y-[1px]"
                onClick={() => navigate("/settings")}
              >
                Manage Defaults
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Content */}
      <main className="mx-auto max-w-7xl px-6 py-6">
        {/* Stats */}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          {stats.map((s) => (
            <StatCard key={s.label} {...s} />
          ))}
        </div>

        {/* Panels */}
        <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-3">
          {/* Recent activity */}
          <section className="lg:col-span-2 rounded-2xl border border-gray-200 bg-white p-5 shadow-sm">
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-base font-semibold">Recent Activity</h2>
              <Link className="text-sm text-blue-600 hover:underline" to="/submissions">
                View all
              </Link>
            </div>

            <div className="overflow-hidden rounded-xl border">
              <table className="min-w-full divide-y divide-gray-200 text-sm">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-4 py-2 text-left font-medium text-gray-600">ID</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-600">Form</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-600">User</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-600">Status</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-600">Time</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {recent.map((r) => (
                    <tr key={r.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2 font-medium">{r.id}</td>
                      <td className="px-4 py-2">{r.form}</td>
                      <td className="px-4 py-2">{r.user}</td>
                      <td className="px-4 py-2">
                        <Badge status={r.status} />
                      </td>
                      <td className="px-4 py-2 text-gray-500">{r.ts}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* Shortcuts / Help */}
          <aside className="rounded-2xl border border-gray-200 bg-white p-5 shadow-sm">
            <h2 className="mb-3 text-base font-semibold">Shortcuts</h2>
            <ul className="space-y-2 text-sm">
              <li>
                <Link className="text-blue-600 hover:underline" to="/forms">
                  Fill a form
                </Link>
              </li>
              <li>
                <Link className="text-blue-600 hover:underline" to="/fill">
                  Start new submission
                </Link>
              </li>
              <li>
                <Link className="text-blue-600 hover:underline" to="/submissions">
                  Submission history
                </Link>
              </li>
              <li>
                <Link className="text-blue-600 hover:underline" to="/settings">
                  User defaults
                </Link>
              </li>
              <li>
                <Link className="text-blue-600 hover:underline" to="/sign">
                  Sign a form
                </Link>
              </li>
            </ul>

            <div className="mt-5 rounded-xl bg-gray-50 p-4 text-xs text-gray-600">
              Tip: set <code>editor.defaultFormatter</code> and <code>editor.formatOnSave</code> in VS Code for tidy edits.
            </div>
          </aside>
        </div>
      </main>
    </div>
  );
}
