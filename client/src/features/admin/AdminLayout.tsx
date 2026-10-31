import React, { useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  DollarSign,
  ExternalLink,
  Inbox,
  LayoutDashboard,
  Lock,
  LogOut,
  Package,
  Settings,
  Sparkles,
} from 'lucide-react';
import { api } from '../../api/client';

export const AdminLayout: React.FC = () => {
  const queryClient = useQueryClient();

  const {
    data: admin,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['admin', 'me'],
    queryFn: api.getAdminMe,
    retry: false,
  });

  const [username, setUsername] = useState('dhara');
  const [password, setPassword] = useState('test');
  const [loginError, setLoginError] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginError(null);
    setIsLoggingIn(true);
    try {
      await api.adminLogin(username, password);
      setPassword('');
      await queryClient.invalidateQueries({ queryKey: ['admin'] });
    } catch (err) {
      setLoginError(err instanceof Error ? err.message : 'Login failed.');
    } finally {
      setIsLoggingIn(false);
    }
  };

  const handleLogout = async () => {
    await api.adminLogout();
    queryClient.removeQueries({ queryKey: ['admin'] });
  };

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-stone-100 text-sm text-stone-600">
        Checking studio session…
      </div>
    );
  }

  if (isError || !admin) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-stone-100 px-4">
        <div className="w-full max-w-md rounded-3xl border border-stone-200 bg-white p-8 shadow-sm">
          <div className="flex items-center gap-2.5">
            <span className="flex h-10 w-10 items-center justify-center rounded-full bg-amber-900 text-amber-50">
              <Lock className="h-4 w-4" />
            </span>
            <div>
              <h1 className="text-2xl font-bold text-stone-900">Studio Admin Sign In</h1>
              <p className="text-xs text-stone-500">Resin Art by Dhara · Management Console</p>
            </div>
          </div>

          {loginError && (
            <div className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-xs font-medium text-red-700">
              {loginError}
            </div>
          )}

          <form onSubmit={handleLogin} className="mt-5 space-y-4">
            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-stone-700">
                Username
              </label>
              <input
                type="text"
                required
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2.5 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-stone-700">
                Password
              </label>
              <input
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter studio password"
                className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2.5 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
              />
              <p className="mt-1 text-[11px] text-stone-400">
                Default dev credentials: <code>dhara</code> / <code>ResinArt@2026</code>
              </p>
            </div>

            <button
              type="submit"
              disabled={isLoggingIn}
              className="w-full rounded-full bg-stone-900 py-3 text-sm font-semibold text-white hover:bg-amber-900 disabled:opacity-60"
            >
              {isLoggingIn ? 'Signing in…' : 'Sign In to Admin Panel'}
            </button>
          </form>

          <div className="mt-5 border-t border-stone-100 pt-4 text-center">
            <Link to="/" className="text-xs font-medium text-stone-500 hover:text-stone-900">
              ← Return to public storefront
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const navClass = ({ isActive }: { isActive: boolean }) =>
    `inline-flex items-center gap-2 rounded-full px-4 py-2 text-xs font-semibold transition ${
      isActive
        ? 'bg-amber-900 text-white shadow-2xs'
        : 'text-stone-700 hover:bg-stone-200/70 hover:text-stone-900'
    }`;

  return (
    <div className="min-h-screen bg-stone-100">
      <header className="sticky top-0 z-30 border-b border-stone-200 bg-white">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-amber-900 text-amber-50">
              <Sparkles className="h-4 w-4" />
            </span>
            <div>
              <span className="block text-base font-bold text-stone-900">
                Studio Admin · {admin.displayName}
              </span>
              <span className="block text-[11px] text-stone-500">
                Catalogue · Regional Pricing · Leads · appsettings.json
              </span>
            </div>
          </div>

          <nav className="flex flex-wrap items-center gap-1.5">
            <NavLink to="/admin" end className={navClass}>
              <LayoutDashboard className="h-3.5 w-3.5" /> Overview
            </NavLink>
            <NavLink to="/admin/products" className={navClass}>
              <Package className="h-3.5 w-3.5" /> Products
            </NavLink>
            <NavLink to="/admin/pricing" className={navClass}>
              <DollarSign className="h-3.5 w-3.5" /> Pricing
            </NavLink>
            <NavLink to="/admin/inbox" className={navClass}>
              <Inbox className="h-3.5 w-3.5" /> Leads Inbox
            </NavLink>
            <NavLink to="/admin/settings" className={navClass}>
              <Settings className="h-3.5 w-3.5" /> Configuration
            </NavLink>
          </nav>

          <div className="flex items-center gap-2">
            <Link
              to="/"
              className="inline-flex items-center gap-1 rounded-full border border-stone-300 px-3 py-1.5 text-xs font-medium text-stone-700 hover:border-stone-500"
            >
              Storefront <ExternalLink className="h-3 w-3" />
            </Link>
            <button
              type="button"
              onClick={() => void handleLogout()}
              className="inline-flex items-center gap-1 rounded-full border border-stone-300 px-3 py-1.5 text-xs font-medium text-stone-700 hover:border-red-400 hover:text-red-700"
            >
              <LogOut className="h-3.5 w-3.5" /> Sign out
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <Outlet />
      </main>
    </div>
  );
};

export const AdminDashboardPage: React.FC = () => {
  const { data: stats } = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: api.getAdminDashboard,
  });

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-stone-900">Studio Dashboard</h1>
        <p className="mt-1 text-sm text-stone-600">
          Manage resin art products, regional pricing for Gujarat (INR), USA (USD) &amp; Canada
          (CAD), customer enquiries, and application settings.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="rounded-2xl border border-stone-200 bg-white p-5">
          <span className="text-xs font-semibold uppercase tracking-wider text-stone-500">
            Published Products
          </span>
          <div className="mt-2 text-3xl font-bold text-stone-900">
            {stats?.publishedProducts ?? '—'}
          </div>
          <p className="mt-1 text-xs text-stone-500">
            {stats?.draftProducts ?? 0} drafts · {stats?.archivedProducts ?? 0} archived
          </p>
        </div>

        <div className="rounded-2xl border border-stone-200 bg-white p-5">
          <span className="text-xs font-semibold uppercase tracking-wider text-stone-500">
            New Custom Requests
          </span>
          <div className="mt-2 text-3xl font-bold text-amber-900">
            {stats?.newCustomRequests ?? '—'}
          </div>
          <p className="mt-1 text-xs text-stone-500">
            {stats?.totalCustomRequests ?? 0} total custom requests
          </p>
        </div>

        <div className="rounded-2xl border border-stone-200 bg-white p-5">
          <span className="text-xs font-semibold uppercase tracking-wider text-stone-500">
            New Contact Enquiries
          </span>
          <div className="mt-2 text-3xl font-bold text-emerald-700">
            {stats?.newContacts ?? '—'}
          </div>
          <p className="mt-1 text-xs text-stone-500">
            {stats?.totalContacts ?? 0} total contacts logged
          </p>
        </div>

        <div className="rounded-2xl border border-stone-200 bg-white p-5">
          <span className="text-xs font-semibold uppercase tracking-wider text-stone-500">
            Region Mismatch Alerts
          </span>
          <div className="mt-2 flex items-center gap-2 text-3xl font-bold text-stone-900">
            {stats?.regionMismatches ?? 0}
            {(stats?.regionMismatches ?? 0) > 0 && (
              <AlertTriangle className="h-5 w-5 text-amber-600" />
            )}
          </div>
          <p className="mt-1 text-xs text-stone-500">
            Leads where selected region differed from IP country
          </p>
        </div>
      </div>

      <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-4">
        <Link
          to="/admin/products"
          className="rounded-2xl border border-stone-200 bg-white p-6 transition hover:border-amber-700 hover:shadow-xs"
        >
          <Package className="h-6 w-6 text-amber-800" />
          <h2 className="mt-3 text-xl font-bold text-stone-900">Product Management</h2>
          <p className="mt-1 text-xs text-stone-600">
            Upload new resin art pieces, manage multiple photos, configure custom options, or
            archive old items.
          </p>
        </Link>

        <Link
          to="/admin/pricing"
          className="rounded-2xl border border-stone-200 bg-white p-6 transition hover:border-amber-700 hover:shadow-xs"
        >
          <DollarSign className="h-6 w-6 text-amber-800" />
          <h2 className="mt-3 text-xl font-bold text-stone-900">Regional Pricing Matrix</h2>
          <p className="mt-1 text-xs text-stone-600">
            Edit Gujarat (INR), USA (USD) and Canada (CAD) prices, derivation rules, extras, and
            shipping notes.
          </p>
        </Link>

        <Link
          to="/admin/inbox"
          className="rounded-2xl border border-stone-200 bg-white p-6 transition hover:border-amber-700 hover:shadow-xs"
        >
          <Inbox className="h-6 w-6 text-amber-800" />
          <h2 className="mt-3 text-xl font-bold text-stone-900">Customer Leads Inbox</h2>
          <p className="mt-1 text-xs text-stone-600">
            View contact form submissions &amp; custom requests, open WhatsApp chats, or export to
            CSV.
          </p>
        </Link>

        <Link
          to="/admin/settings"
          className="rounded-2xl border border-stone-200 bg-white p-6 transition hover:border-amber-700 hover:shadow-xs"
        >
          <Settings className="h-6 w-6 text-amber-800" />
          <h2 className="mt-3 text-xl font-bold text-stone-900">Configuration (appsettings)</h2>
          <p className="mt-1 text-xs text-stone-600">
            Update WhatsApp number, default message, regional defaults, and feature toggles with
            live reload.
          </p>
        </Link>
      </div>
    </div>
  );
};
