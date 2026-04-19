import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import HomePage from './pages/HomePage';
import AuthCallback from './auth/AuthCallback';
import ProtectedRoute from './auth/ProtectedRoute';
import SampleListPage from './pages/samples/SampleListPage';
import SampleDetailPage from './pages/samples/SampleDetailPage';
import Sample2ListPage from './pages/sample2s/Sample2ListPage';
import Sample2DetailPage from './pages/sample2s/Sample2DetailPage';
import SampleHistoryPage, { Sample2HistoryPage } from './pages/history/EntityHistoryPage';
import ScenarioListPage from './pages/chaos-testing/ScenarioListPage';
import ScenarioRunPage from './pages/chaos-testing/ScenarioRunPage';
import AnalyticsPage from './pages/analytics/AnalyticsPage';
import { NotificationProvider } from './notifications/NotificationProvider';
import AdminRoute from './auth/AdminRoute';

export default function App() {
  return (
    <BrowserRouter>
      <NotificationProvider>
        <Layout>
          <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/auth/callback" element={<AuthCallback />} />
          <Route
            path="/samples"
            element={<ProtectedRoute><SampleListPage /></ProtectedRoute>}
          />
          <Route
            path="/samples/:id"
            element={<ProtectedRoute><SampleDetailPage /></ProtectedRoute>}
          />
          <Route
            path="/samples/:id/history"
            element={<ProtectedRoute><SampleHistoryPage /></ProtectedRoute>}
          />
          <Route
            path="/sample2s"
            element={<ProtectedRoute><Sample2ListPage /></ProtectedRoute>}
          />
          <Route
            path="/sample2s/:id"
            element={<ProtectedRoute><Sample2DetailPage /></ProtectedRoute>}
          />
          <Route
            path="/sample2s/:id/history"
            element={<ProtectedRoute><Sample2HistoryPage /></ProtectedRoute>}
          />
          <Route
            path="/analytics"
            element={<ProtectedRoute><AnalyticsPage /></ProtectedRoute>}
          />
          <Route
            path="/chaos-testing"
            element={<AdminRoute><ScenarioListPage /></AdminRoute>}
          />
          <Route
            path="/chaos-testing/run/:runId"
            element={<AdminRoute><ScenarioRunPage /></AdminRoute>}
          />
        </Routes>
        </Layout>
      </NotificationProvider>
    </BrowserRouter>
  );
}
