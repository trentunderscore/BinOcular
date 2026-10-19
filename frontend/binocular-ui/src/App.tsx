import React from 'react';

function App() {

  // Create a place to display the output from the backend
  const [output, setOutput] = React.useState<string>('');

  const handleFileUpload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      // Upload to backend
      const formData = new FormData();
      formData.append('file', file);

      fetch('/api/upload', {
        method: 'POST',
        body: formData,
      })
        .then(response => response.json())
        .then(data => {
          setOutput(JSON.stringify(data));
          console.log('Success:' + JSON.stringify(data));
        })
        .catch((error) => {
          console.error('Error:', error);
        });
    }
  }


  return (
    <>
      <div className="flex flex-col items-center justify-center min-h-screen bg-slate-900 text-white">
        <input className="mb-4 bg-slate-800 text-white placeholder:text-slate-400 border border-slate-600 rounded-md py-2 px-4 focus:outline-none focus:ring-2 focus:ring-blue-500" type="file" onChange={handleFileUpload} />

        <div>
          <h1 className="text-2xl font-bold mb-4">Output</h1>
          <div className="bg-slate-800 text-white p-4 rounded-md">
            {output.split('\n').map((line, index) => (
              <p key={index}>{line}</p>
            ))}
          </div>
        </div>


      </div>

    </>
  )
}

export default App
