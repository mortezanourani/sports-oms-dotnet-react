import { useMemo, useState } from 'react';
import { Typography, Select, InputNumber, Table, Card, Spin } from 'antd';
import { Line } from '@ant-design/plots';
import { usePublicAthleteStats, usePublicProvinces } from '../../hooks/usePublicStats';
import { GENDER_LABELS, PERSIAN_MONTHS } from '../../constants/labels';

const { Title } = Typography;

export default function AthleteStats() {
    const [year, setYear] = useState(new Date().getFullYear());
    const [provinceName, setProvinceName] = useState(undefined);

    const { data: provinces } = usePublicProvinces();
    const { data: rows, isLoading } = usePublicAthleteStats({ year, provinceName });

    const chartData = useMemo(() => {
        if (!rows) return [];
        const grouped = {};
        for (const row of rows) {
            const key = `${row.month}__${row.gender}`;
            grouped[key] = grouped[key] || { month: PERSIAN_MONTHS[row.month - 1], gender: row.gender, count: 0 };
            grouped[key].count += row.count;
        }
        return Object.values(grouped);
    }, [rows]);

    const columns = [
        { title: 'فدراسیون', dataIndex: 'federationName' },
        { title: 'استان', dataIndex: 'provinceName' },
        { title: 'شهر', dataIndex: 'cityName' },
        { title: 'ماه', dataIndex: 'month', render: (v) => PERSIAN_MONTHS[v - 1] },
        { title: 'جنسیت', dataIndex: 'gender', render: (v) => GENDER_LABELS[v] || v },
        { title: 'تعداد', dataIndex: 'count' },
    ];

    return (
        <div>
            <Title level={3}>آمار ورزشکاران</Title>

            <div style={{ display: 'flex', gap: 16, marginBottom: 24 }}>
                <InputNumber placeholder="سال" value={year} onChange={setYear} style={{ width: 120 }} />
                <Select
                    placeholder="فیلتر بر اساس استان" allowClear style={{ width: 220 }}
                    value={provinceName} onChange={setProvinceName}
                    options={provinces?.map((p) => ({ value: p.name, label: p.name }))}
                />
            </div>

            <Spin spinning={isLoading}>
                <Card style={{ marginBottom: 24 }}>
                    <Line data={chartData} xField="month" yField="count" seriesField="gender" />
                </Card>

                <Table
                    rowKey={(r, i) => i}
                    columns={columns}
                    dataSource={rows}
                    pagination={{ pageSize: 10 }}
                />
            </Spin>
        </div>
    );
}
