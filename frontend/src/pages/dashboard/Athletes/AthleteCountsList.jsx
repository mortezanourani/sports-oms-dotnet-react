import { Tabs, Select, Typography } from 'antd';
import { useGeoSections } from '../../../hooks/useGeoSections';
import AthleteCountsTable from './AthleteCountsTable';

const { Title } = Typography;

export default function AthleteCountsList() {
  const geo = useGeoSections();

  const items = [
    geo.showMastery && { key: 'mastery', label: 'کل کشور', children: <AthleteCountsTable filters={{}} /> },
    geo.showProvinceSection && {
      key: 'province', label: 'استان',
      children: (
          <>
            <Select
                placeholder="انتخاب استان" style={{ width: 220, marginBottom: 16 }}
                disabled={geo.provinceLocked}
                value={geo.selectedProvinceId}
                onChange={geo.setSelectedProvinceId}
                options={geo.provinceOptions?.map((p) => ({ value: p.id, label: p.name }))}
            />
            <AthleteCountsTable filters={{ provinceId: geo.selectedProvinceId }} />
          </>
      ),
    },
    {
      key: 'city', label: 'شهر',
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
            <AthleteCountsTable filters={{ cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId }} />
          </>
      ),
    },
  ].filter(Boolean);

  return (
      <div>
        <Title level={4}>آمار ورزشکاران</Title>
        <Tabs items={items} />
      </div>
  );
}
