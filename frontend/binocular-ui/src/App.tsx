import React from 'react';
import type { AnalysisResponse } from './api/types';

function App() {

  // Create a place to display the output from the backend
  const [output, setOutput] = React.useState<AnalysisResponse[]>([]);

  const handleFileUpload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      // Upload to backend
      const formData = new FormData();
      formData.append('file', file);

      fetch('/api/analyses', {
        method: 'POST',
        body: formData,
      })
        .then(response => response.json())
        .then(data => {
          setOutput(prevOutput => [...prevOutput, data]);
          console.log('Success:' + JSON.stringify(data));
        })
        .catch((error) => {
          console.error('Error:', error);
        });
    }
  }

  const getToolResults = (analysisId: string) => {
    fetch(`/api/analyses/${analysisId}/tool-results`)
      .then(response => response.json())
      .then(data => {
        console.log('Tool Results:', data);
      })
      .catch((error) => {
        console.error('Error fetching tool results:', error);
      });
  }




  return (
    <>
      <div className="flex flex-col items-center justify-center min-h-screen bg-zinc-900 text-white w-full h-full p-4">
        <h1 className="text-3xl font-bold mb-4">Binocular UI</h1>
        <div className="flex flex-col items-center justify-center p-4 bg-zinc-800 rounded-lg shadow-md w-full">

          <input
            type="file"
            accept=".exe,.dll,.bin,.elf,.so,.o,.a,.lib,.img,.iso"
            onChange={handleFileUpload}
            className="mb-4 p-2 bg-zinc-700 rounded-lg cursor-pointer"
          />

          <div className="w-full p-4 bg-zinc-700 rounded-lg overflow-y-auto max-h-96">
            <h2 className="text-xl font-semibold mb-2">Output:</h2>
            {output.length === 0 ? (
              <p>No output yet. Upload a file to see results.</p>
            ) : (
              <ul>
                {output.map((item, index) => (
                  <li key={index} className="mb-2">
                    <div><strong>Analysis ID:</strong> {item.analysisId}</div>
                    <div><strong>File Name:</strong> {item.fileName}</div>
                    <div><strong>Status:</strong> {item.status}</div>
                    <div><strong>SHA256:</strong> {item.sha256}</div>
                    <div><strong>Created At (UTC):</strong> {item.createdAtUtc}</div>
                    <button
                      onClick={() => getToolResults(item.analysisId)}
                      className="mt-2 p-2 bg-blue-600 rounded-lg hover:bg-blue-700"
                    >
                      Get Tool Results
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>

    </>
  )
}

export default App
