import React, { useState, useEffect } from 'react';
import { createRoot } from 'react-dom/client';

declare global {
  interface Window {
    electronAPI: {
      hideOverlay: () => Promise<void>;
      stopAlert: () => Promise<void>;
      onShowAlert: (cb: (data: any) => void) => () => void;
    };
  }
}

interface AlertData {
  message: string;
  backgroundColor: string;
  textColor: string;
}

function Overlay() {
  const [alertData, setAlertData] = useState<AlertData | null>(null);

  useEffect(() => {
    const cleanup = window.electronAPI.onShowAlert((data: AlertData) => {
      setAlertData(data);
    });
    return cleanup;
  }, []);

  const handleClick = () => {
    // Stop the alert (hides overlay + stops audio + opens URL)
    window.electronAPI.stopAlert();
  };

  if (!alertData) return null;

  return (
    <div
      style={{
        width: '100vw',
        height: '100vh',
        background: alertData.backgroundColor,
        color: alertData.textColor,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        cursor: 'pointer',
        fontFamily: '"TsukuhouShogoMin", sans-serif',
        fontSize: '42px',
        fontWeight: 'bold',
        letterSpacing: '0.1em',
        overflow: 'hidden',
        whiteSpace: 'nowrap',
        userSelect: 'none',
      }}
      onClick={handleClick}
      title="クリックで閉じる"
    >
      <div style={{ animation: 'scroll 10s linear infinite' }}>
        {alertData.message}
      </div>
      <style>{`
        @keyframes scroll {
          0% { transform: translateX(100vw); }
          100% { transform: translateX(-100%); }
        }
      `}</style>
    </div>
  );
}

const root = createRoot(document.getElementById('overlay-root')!);
root.render(<Overlay />);
