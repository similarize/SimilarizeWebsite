<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
    <title>Loading RC Rally Jump</title>
    <script type="text/javascript">
        // Enforce title immediately and during loading
        document.title = "Loading RC Rally Jump";
        window.onload = function() {
            document.title = "Loading RC Rally Jump";
        };
        // Periodically check to prevent overrides
        setInterval(function() {
            if (document.title !== "RC Rally Jump") {
                document.title = "Loading RC Rally Jump";
            }
        }, 100);
    </script>
    <style>
        body { margin: 0; display: flex; justify-content: center; align-items: center; height: 100vh; background: #000; }
        canvas { display: block; }
        #loading { position: absolute; color: white; font-family: Arial, sans-serif; font-size: 24px; }
    </style>
</head>
<body>
    <div id="loading">Loading RC Rally Jump...</div>
    <script type="module" src="./main.py.js"></script>
</body>
</html>