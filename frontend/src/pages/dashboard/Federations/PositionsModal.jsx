import { useState } from 'react';
import { Modal, Table, Tag, Button } from 'antd';
import { useFederationPositions } from '../../../hooks/useFederations';
import { FEDERATION_POSITION_LABELS } from '../../../constants/labels';
import AddPositionModal from './AddPositionModal';
import { formatSolarDate } from '../../../utils/dates';

export default function PositionsModal({ federation, open, onClose }) {
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);

  const { data: positions, isLoading, refetch } = useFederationPositions(federation?.id);

  const columns = [
    { title: 'سمت', dataIndex: 'position', render: (v) => FEDERATION_POSITION_LABELS[v] || v },
    {
      title: 'نام',
      render: (_, record) => [record.userFirstName, record.userLastName].filter(Boolean).join(' ') || '—',
    },
    { title: 'از تاریخ', dataIndex: 'startDate', render: formatSolarDate },
    {
      title: 'تا تاریخ', dataIndex: 'endDate',
      render: (v) => (v ? formatSolarDate(v) : <Tag color="green">در حال حاضر</Tag>),
    },
  ];

  const handleAddPosition = () => {
    setIsAddModalOpen(true);
  };

  const handleAddModalClose = () => {
    setIsAddModalOpen(false);
    // Refetch positions after closing the add modal
    refetch();
  };

  return (
    <>
      <Modal
        title={federation ? `سوابق مسئولین فدراسیون ${federation.displayName}` : 'سوابق مسئولین'}
        open={open}
        onCancel={onClose}
        footer={null}
        width={700}
      >
        <div style={{ marginBottom: 16 }}>
          <Button type="primary" onClick={handleAddPosition}>
            افزودن سمت جدید
          </Button>
        </div>
        <Table
          rowKey="id"
          columns={columns}
          dataSource={positions?.slice().sort((a, b) => new Date(b.startDate) - new Date(a.startDate))}
          loading={isLoading}
          pagination={false}
          locale={{ emptyText: 'سابقه‌ای ثبت نشده است' }}
        />
        <div style={{ marginTop: 12, color: '#888', fontSize: 13 }}>
          برای تغییر مسئولین، به بخش «کاربران» مراجعه کنید.
        </div>
      </Modal>
      <AddPositionModal 
        federation={federation} 
        open={isAddModalOpen} 
        onClose={handleAddModalClose} 
      />
    </>
  );
}
