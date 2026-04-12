import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import HomePage from './pages/HomePage';
import AuthCallback from './auth/AuthCallback';
import ProtectedRoute from './auth/ProtectedRoute';
import SampleListPage from './pages/samples/SampleListPage';
import SampleDetailPage from './pages/samples/SampleDetailPage';
import Sample2ListPage from './pages/sample2s/Sample2ListPage';
import Sample2DetailPage from './pages/sample2s/Sample2DetailPage';

export default function App() {
  return (
    <BrowserRouter>
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
            path="/sample2s"
            element={<ProtectedRoute><Sample2ListPage /></ProtectedRoute>}
          />
          <Route
            path="/sample2s/:id"
            element={<ProtectedRoute><Sample2DetailPage /></ProtectedRoute>}
          />
        </Routes>
      </Layout>
    </BrowserRouter>
  );
}
