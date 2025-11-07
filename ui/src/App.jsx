// src/App.jsx
import React from "react";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard"; // if you made Dashboard.tsx, this import still works in Vite

export default function App() {
  // TODO: replace with your real auth check (cookie/token/ctx)
  console.log("App.jsx mounted"); // quick sanity check
  const isAuthed = false;

  return (
    <BrowserRouter>
      <Routes>
        {/* Start app at /login if not authenticated */}
        <Route
          path="/"
          element={isAuthed ? <Navigate to="/dashboard" replace /> : <Navigate to="/login" replace />}
        />
        <Route path="/login" element={<Login />} />
        <Route path="/dashboard" element={<Dashboard />} />
        {/* add more routes here, e.g. forms, submissions, settings */}
      </Routes>
    </BrowserRouter>
  );
}
