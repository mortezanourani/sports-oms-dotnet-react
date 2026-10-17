import { useState } from 'react';
import { Select, Space, Typography } from 'antd';
import { useGeoSections } from '../../../hooks/useGeoSections';
import FacilitiesTable from './FacilitiesTable';

const { Title } = Typography;

export default function FacilitiesList({ facilityTypes, title }) {
    const geo = useGeoSections();
    const [page, setPage] = useState(1);

    const handleProvinceChange = (provinceId) => {
        geo.setSelectedProvinceId(provinceId);
        geo.setSelectedCityId(null);
        setPage(1);
    };

    const handleCityChange = (cityId) => {
        geo.setSelectedCityId(cityId);
        setPage(1);
    };

    return (
        <div>
            <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>{title}</Title>
            <FacilitiesTable
                filters={{
                    provinceId: geo.selectedProvinceId,
                    cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId,
                }}
                facilityTypes={facilityTypes}
                page={page}
                onPageChange={setPage}
                renderLocationFilters={(facilityTypeFilter) => (
                    <Space wrap>
                        {geo.auth.scopeLevel !== 'City' && (
                            <>
                                {geo.auth.scopeLevel === 'Mastery' && (
                                    <Select
                                        placeholder="فیلتر بر اساس استان"
                                        allowClear
                                        style={{ width: 220 }}
                                        value={geo.selectedProvinceId}
                                        onChange={handleProvinceChange}
                                        options={geo.provinceOptions?.map((p) => ({ value: p.id, label: p.name }))}
                                    />
                                )}
                                <Select
                                    placeholder={geo.selectedProvinceId ? 'فیلتر بر اساس شهر' : 'ابتدا استان را انتخاب کنید'}
                                    allowClear
                                    style={{ width: 220 }}
                                    disabled={!geo.selectedProvinceId}
                                    value={geo.selectedCityId}
                                    onChange={handleCityChange}
                                    options={geo.cityOptions?.map((c) => ({ value: c.id, label: c.name }))}
                                />
                            </>
                        )}
                        {facilityTypeFilter}
                    </Space>
                )}
            />
        </div>
    );
}
