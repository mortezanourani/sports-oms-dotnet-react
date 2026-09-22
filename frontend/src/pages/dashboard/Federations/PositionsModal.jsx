import { Modal, Table, Tag } from 'antd';
import { useFederationPositions } from '../../../hooks/useFederations';
import { FEDERATION_POSITION_LABELS } from '../../../constants/labels';

export default function PositionsModal({ federation, open, onClose }) {
  const { data: positions, isLoading } = useFederationPositions(federation?.id);

  const columns = [
    { title: 'سمت', dataIndex: 'position', render: (v) => FEDERATION_POSITION_LABELS[v] || v },
    { title: 'نام', dataIndex: 'userFullName' },
    { title: 'از تاریخ', dataIndex: 'startDate' },
    {
      title: 'تا تاریخ', dataIndex: 'endDate',
      render: (v) => (v ? v : <Tag color="green">در حال حاضر</Tag>),
    },
  ];

  return (
    <Modal
      title={federation ? `سوابق مسئولین فدراسیون ${federation.displayName}` : 'سوابق مسئولین'}
      open={open}
      onCancel={onClose}
      footer={null}
      width={700}
    >
      <Table
        rowKey="id"
        columns={columns}
        dataSource={positions}
        loading={isLoading}
        pagination={false}
        locale={{ emptyText: 'سابقه‌ای ثبت نشده است' }}
      />
      <div style={{ marginTop: 12, color: '#888', fontSize: 13 }}>
        برای تغییر مسئولین، به بخش «کاربران» مراجعه کنید.
      </div>
    </Modal>
  );
}
