// src/App.jsx
import React from "react";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import Login from "./pages/login";
import Dashboard from "./pages/Dashboard"; // if you made Dashboard.tsx, this import still works in Vite
import Signatures from "./pages/Signatures";
import Forms from "./pages/Forms";
import Fill from "./pages/Fill";
import { api } from "./api";

export default function App() {
  // Use real auth check via /api/auth/me
  const [isAuthed, setIsAuthed] = React.useState(null);

  React.useEffect(() => {
    api
      .get("/auth/me")
      .then(() => setIsAuthed(true))
      .catch(() => setIsAuthed(false));
  }, []);

  //temp to bypass login, uncomment above for real auth
  // const [isAuthed, setIsAuthed] = React.useState({ userName: "admin", fullName: "admin User" });
  // setIsAuthed(true)

  if (isAuthed === null) return null; // or a small loader

  return (
    <BrowserRouter>
      <Routes>
        {/* Start app at /login if not authenticated */}
        {/* <Route path="/" element={<Dashboard />} /> */}
        <Route
          path="/"
          element={isAuthed ? <Navigate to="/forms" replace /> : <Navigate to="/login" replace />}
        />
        <Route path="/login" element={<Login />} />
        {/* <Route path="/dashboard" element={<Dashboard />} /> */}
        <Route
          path="/dashboard"
          element={isAuthed ? <Dashboard /> : <Navigate to="/login" replace />}
        />
        <Route
          path="/forms"
          element={isAuthed ? <Forms /> : <Navigate to="/login" replace />}
        />
        <Route path="/fill" element={<Fill />} />
        <Route path="/fill/:slug" element={<Fill />} />
        <Route path="/sign" element={<Signatures />} />
        <Route path="/sign/:slug" element={<Signatures />} />
        {/* add more routes here, e.g. forms, submissions, settings */}
      </Routes>
    </BrowserRouter>
  );
}
