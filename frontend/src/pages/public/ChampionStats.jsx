import { useState } from 'react';
import { Typography, Select, Row, Col, Card, Avatar, Tag, Pagination, Empty } from 'antd';
import { TrophyOutlined } from '@ant-design/icons';
import { usePublicChampions } from '../../hooks/usePublicStats';
import { usePublicFederations } from '../../hooks/usePublicStats';
import { GENDER_LABELS } from '../../constants/labels';

const { Title, Text } = Typography;

export default function ChampionStats() {
    const [federationId, setFederationId] = useState(undefined);
    const [page, setPage] = useState(1);

    const { data: federations } = usePublicFederations();
    const { data, isLoading } = usePublicChampions({ federationId, page, pageSize: 12 });

    return (
        <div>
            <Title level={3}>قهرمانان</Title>

            <Select
                placeholder="فیلتر بر اساس فدراسیون" allowClear style={{ width: 220, marginBottom: 24 }}
                value={federationId} onChange={setFederationId}
                options={federations?.map((f) => ({ value: f.id, label: f.name }))}
            />

            {!isLoading && data?.items?.length === 0 && <Empty description="قهرمانی یافت نشد" />}

            <Row gutter={[16, 16]}>
                {data?.items?.map((champion) => (
                    <Col xs={24} sm={12} md={8} lg={6} key={champion.id}>
                        <Card>
                            <div style={{ textAlign: 'center', marginBottom: 12 }}>
                                <Avatar
                                    size={80}
                                    src={champion.photoUrl ? `${import.meta.env.VITE_API_ORIGIN}${champion.photoUrl}` : undefined}
                                    icon={<TrophyOutlined />}
                                />
                            </div>
                            <Title level={5} style={{ textAlign: 'center', marginBottom: 4 }}>{champion.fullName}</Title>
                            <Text type="secondary" style={{ display: 'block', textAlign: 'center', marginBottom: 12 }}>
                                {champion.federationName} — {GENDER_LABELS[champion.gender] || champion.gender}
                            </Text>
                            <div style={{ textAlign: 'center' }}>
                                {champion.goldCount > 0 && <Tag color="gold">طلا: {champion.goldCount}</Tag>}
                                {champion.silverCount > 0 && <Tag color="default">نقره: {champion.silverCount}</Tag>}
                                {champion.bronzeCount > 0 && <Tag color="volcano">برنز: {champion.bronzeCount}</Tag>}
                            </div>
                        </Card>
                    </Col>
                ))}
            </Row>

            <div style={{ display: 'flex', justifyContent: 'center', marginTop: 24 }}>
                <Pagination current={page} pageSize={12} total={data?.count} onChange={setPage} />
            </div>
        </div>
    );
}
