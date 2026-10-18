import ReactDOM from 'react-dom/client';
import { ConfigProvider } from 'antd';
import faIR from 'antd/locale/fa_IR';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import '@fontsource/vazirmatn';

const queryClient = new QueryClient();

ReactDOM.createRoot(document.getElementById('root')).render(
    <ConfigProvider
        direction="rtl"
        locale={faIR}
        theme={{
            token: {
                fontFamily: 'Vazirmatn, Arial, sans-serif',
            },
        }}
    >
        <QueryClientProvider client={queryClient}>
            <BrowserRouter>
                <App />
            </BrowserRouter>
        </QueryClientProvider>
    </ConfigProvider>
);
