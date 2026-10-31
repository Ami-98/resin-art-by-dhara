import React from 'react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RegionProvider } from './features/region/RegionContext';
import { AppLayout } from './components/AppLayout';
import { HomePage } from './features/catalog/HomePage';
import { ProductGallery } from './features/catalog/ProductGallery';
import { ProductDetail } from './features/catalog/ProductDetail';
import { ContactPage } from './features/contact/ContactPage';
import { PrivacyPage } from './features/legal/PrivacyPage';
import { AdminDashboardPage, AdminLayout } from './features/admin/AdminLayout';
import { AdminProductsPage } from './features/admin/AdminProductsPage';
import { AdminPricingPage } from './features/admin/AdminPricingPage';
import { AdminInboxPage } from './features/admin/AdminInboxPage';
import { AdminSettingsPage } from './features/admin/AdminSettingsPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60 * 1000,
      refetchOnWindowFocus: false,
    },
  },
});

export const App: React.FC = () => {
  return (
    <QueryClientProvider client={queryClient}>
      <RegionProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<AppLayout />}>
              <Route index element={<HomePage />} />
              <Route path="products" element={<ProductGallery />} />
              <Route path="products/:slug" element={<ProductDetail />} />
              <Route path="custom" element={<ProductGallery fixedType="custom" />} />
              <Route path="custom/:slug" element={<ProductDetail />} />
              <Route path="contact" element={<ContactPage />} />
              <Route path="privacy" element={<PrivacyPage />} />
            </Route>
            <Route path="admin" element={<AdminLayout />}>
              <Route index element={<AdminDashboardPage />} />
              <Route path="products" element={<AdminProductsPage />} />
              <Route path="pricing" element={<AdminPricingPage />} />
              <Route path="inbox" element={<AdminInboxPage />} />
              <Route path="settings" element={<AdminSettingsPage />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </RegionProvider>
    </QueryClientProvider>
  );
};
