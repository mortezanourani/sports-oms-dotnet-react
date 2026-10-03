import { Tabs, Select, Typography } from 'antd';
import { useGeoSections } from '../../../hooks/useGeoSections';
import ChampionsTable from './ChampionsTable';

const { Title } = Typography;

export default function ChampionsList() {
    const geo = useGeoSections();

    const items = [
        geo.showMastery && { key: 'mastery', label: 'کل کشور', children: <ChampionsTable filters={{}} /> },
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
                    <ChampionsTable filters={{ provinceId: geo.selectedProvinceId }} />
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
                    <ChampionsTable filters={{ cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId }} />
                </>
            ),
        },
    ].filter(Boolean);

    return (
        <div>
            <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>قهرمانان</Title>
            <Tabs items={items} />
        </div>
    );
}
