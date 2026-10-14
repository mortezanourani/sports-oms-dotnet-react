import { useMemo, useState } from 'react';
import { Typography, Select, Table, Row, Col, Statistic, Card, Spin } from 'antd';
import { Column } from '@ant-design/plots';
import { usePublicFacilityStats } from '../../hooks/usePublicStats';
import { usePublicProvinces } from '../../hooks/usePublicStats';

const { Title } = Typography;
const FACILITY_TYPE_LABELS = { Msy: 'دولتی وزارت ورزش', Private: 'باشگاه خصوصی', Gov: 'دولتی سایر ارگان ها' };

export default function FacilityStats() {
    const [provinceName, setProvinceName] = useState(undefined);
    const { data: provinces } = usePublicProvinces();
    const { data: rows, isLoading } = usePublicFacilityStats({ provinceName });

    // The chart needs one row per (province, type) pair with summed counts
    // across cities — the raw API response is one row per (type, province,
    // city), so this collapses the city dimension away for this specific view.
    const chartData = useMemo(() => {
        if (!rows) return [];
        const grouped = {};
        for (const row of rows) {
            const key = `${row.provinceName}__${row.facilityType}`;
            grouped[key] = grouped[key] || { province: row.provinceName, type: row.facilityType, count: 0 };
            grouped[key].count += row.count;
        }
        return Object.values(grouped);
    }, [rows]);

    const totalsByType = useMemo(() => {
        const totals = { Msy: 0, Private: 0, Gov: 0 };
        rows?.forEach((r) => { totals[r.facilityType] = (totals[r.facilityType] || 0) + r.count; });
        return totals;
    }, [rows]);

    const columns = [
        { title: 'نوع', dataIndex: 'facilityType', render: (v) => FACILITY_TYPE_LABELS[v] || v },
        { title: 'استان', dataIndex: 'provinceName' },
        { title: 'شهر', dataIndex: 'cityName' },
        { title: 'تعداد', dataIndex: 'count' },
    ];

    return (
        <div>
            <Title level={3}>آمار اماکن ورزشی</Title>

            <Select
                placeholder="فیلتر بر اساس استان" allowClear style={{ width: 220, marginBottom: 24 }}
                value={provinceName} onChange={setProvinceName}
                options={provinces?.map((p) => ({ value: p.name, label: p.name }))}
            />

            <Row gutter={16} style={{ marginBottom: 24 }}>
                <Col span={8}><Card><Statistic title="اماکن دولتی وزارت ورزش" value={totalsByType.Msy} /></Card></Col>
                <Col span={8}><Card><Statistic title="اماکن باشگاه خصوصی" value={totalsByType.Private} /></Card></Col>
                <Col span={8}><Card><Statistic title="اماکن دولتی سایر ارگان ها" value={totalsByType.Gov} /></Card></Col>
            </Row>

            <Spin spinning={isLoading}>
                <Card style={{ marginBottom: 24 }}>
                    <Column
                        data={chartData}
                        xField="province"
                        yField="count"
                        colorField="type"
                        group={true}
                        legend={{ position: 'top' }}
                    />
                </Card>

                <Table rowKey={(r) => `${r.facilityType}-${r.provinceName}-${r.cityName}`} columns={columns} dataSource={rows} pagination={{ pageSize: 10 }} />
            </Spin>
        </div>
    );
}
