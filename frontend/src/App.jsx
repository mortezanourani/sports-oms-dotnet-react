import { Routes, Route } from 'react-router-dom';
import PublicLayout from './layouts/PublicLayout';
import DashboardLayout from './layouts/DashboardLayout';
import ProtectedRoute from './components/ProtectedRoute';
import Login from './pages/auth/Login';
import AthleteStats from './pages/public/AthleteStats';
import ChampionStats from './pages/public/ChampionStats';
import FacilityStats from './pages/public/FacilityStats';
import FacilitiesList from './pages/dashboard/Facilities/FacilitiesList';
import LocationsPage from './pages/dashboard/Locations/LocationsPage';
import ChampionsList from './pages/dashboard/Champions/ChampionsList';
import FederationsList from './pages/dashboard/Federations/FederationsList';
import AthleteCountsList from './pages/dashboard/Athletes/AthleteCountsList';

export default function App() {
  return (
      <Routes>
        <Route element={<PublicLayout />}>
          <Route path="/" element={<AthleteStats />} />
          <Route path="/champions" element={<ChampionStats />} />
          <Route path="/facilities" element={<FacilityStats />} />
          <Route path="/login" element={<Login />} />
        </Route>

        <Route element={<ProtectedRoute />}>
          <Route element={<DashboardLayout />}>
            <Route path="/dashboard/facilities" element={<FacilitiesList />} />
            <Route path="/dashboard/champions" element={<ChampionsList />} />
            <Route path="/dashboard/federations" element={<FederationsList />} />
            <Route path="/dashboard/athletes" element={<AthleteCountsList />} />
            <Route path="/dashboard/locations" element={<LocationsPage />} />
          </Route>
        </Route>
      </Routes>
  );
}
