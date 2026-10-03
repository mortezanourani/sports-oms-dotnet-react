import { Tabs, Select, Typography } from 'antd';
import { useState } from 'react';
import { useGeoSections } from '../../../hooks/useGeoSections';
import FederationMasteryTable from './FederationMasteryTable'; // the previous nested-expandable table, renamed
import FederationsFlatTable from './FederationsFlatTable';
import FederationFormModal from './FederationFormModal';

const { Title } = Typography;

export default function FederationsList() {
    const geo = useGeoSections();
    const [createOpen, setCreateOpen] = useState(false);

    const items = [
        geo.showMastery && {
            key: 'mastery', label: 'کل کشور',
            children: <FederationMasteryTable />,
        },
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
                    <FederationsFlatTable level="Provincial" provinceId={geo.selectedProvinceId} />
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
                    <FederationsFlatTable
                        level="City"
                        provinceId={geo.selectedProvinceId}
                        cityId={geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId}
                    />
                </>
            ),
        },
    ].filter(Boolean);

    return (
        <div>
            <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>فدراسیون‌ها</Title>
            <Tabs items={items} />
            <FederationFormModal level="National" open={createOpen} onClose={() => setCreateOpen(false)} />
        </div>
    );
}
