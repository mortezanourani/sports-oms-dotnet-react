import { Tabs, Select, Typography } from 'antd';
import { useGeoSections } from '../../../hooks/useGeoSections';
import UsersTable from './UsersTable';

const { Title } = Typography;

export default function UsersList() {
  const geo = useGeoSections();

  const items = [
    geo.showMastery && {
      key: 'mastery', label: 'وزارت',
      children: <UsersTable filters={{ scopeLevel: 'Mastery' }} />,
    },
    geo.showProvinceSection && {
      key: 'province', label: 'اداره کل',
      children: (
          <>
            {/* Hidden entirely (not just disabled) for a Province-scoped
              viewer, matching the City tab's existing pattern — they
              already know which province they're looking at. */}
            {!geo.provinceLocked && (
                <Select
                    placeholder="انتخاب استان" style={{ width: 220, marginBottom: 16 }}
                    value={geo.selectedProvinceId}
                    onChange={geo.setSelectedProvinceId}
                    options={geo.provinceOptions?.map((p) => ({ value: p.id, label: p.name }))}
                />
            )}
            <UsersTable filters={{ scopeLevel: 'Province', provinceId: geo.selectedProvinceId }} />
          </>
      ),
    },
    {
      key: 'city', label: 'شهرستان',
      children: (
          <>
            {!geo.cityLocked && (
                <Select
                    placeholder="ابتدا استان را در بخش «استان» انتخاب کنید"
                    style={{ width: 220, marginBottom: 16 }}
                    disabled={!geo.selectedProvinceId}
                    value={geo.selectedCityId}
                    onChange={geo.setSelectedCityId}
                    options={geo.cityOptions?.map((c) => ({ value: c.id, label: c.name }))}
                />
            )}
            <UsersTable filters={{
              scopeLevel: 'City',
              cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId,
            }} />
          </>
      ),
    },
  ].filter(Boolean);

  return (
      <div>
        <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>کاربران</Title>
        <Tabs items={items} />
      </div>
  );
}
