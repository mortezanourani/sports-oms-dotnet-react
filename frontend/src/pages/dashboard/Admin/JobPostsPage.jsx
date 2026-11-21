import { Typography } from 'antd';
import JobPostsPanel from '../Users/JobPostsPanel';

const { Title } = Typography;

export default function JobPostsPage() {
  return (
    <div>
      <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>پست‌های سازمانی</Title>
      <JobPostsPanel variant="definitions" />
    </div>
  );
}
