window.ProjectDawnLauncherConfig = {
  gameName: "Project Dawn",

  // Unity Build Editor output:
  // Builds/Win64/{ProjectName}{index}/{ProjectName}{index}.exe
  gameExecutablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe",

  // Same launch format as Assets/Editor/MultiplayerTestBuild.cs RunClient().
  gameStartCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build01",
  gameClients: [
    {
      index: 1,
      label: "Client 1",
      executablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe",
      startCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build01",
      protocolUrl: "projectdawn://launch?client=1"
    },
    {
      index: 2,
      label: "Client 2",
      executablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn2\\Project_Dawn2.exe",
      startCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn2\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn2\\Project_Dawn2.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build02",
      protocolUrl: "projectdawn://launch?client=2"
    },
    {
      index: 3,
      label: "Client 3",
      executablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn3\\Project_Dawn3.exe",
      startCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn3\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn3\\Project_Dawn3.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build03",
      protocolUrl: "projectdawn://launch?client=3"
    },
    {
      index: 4,
      label: "Client 4",
      executablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn4\\Project_Dawn4.exe",
      startCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn4\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn4\\Project_Dawn4.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build04",
      protocolUrl: "projectdawn://launch?client=4"
    }
  ],

  customProtocolUrl: "projectdawn://launch",
  customProtocolAllUrl: "projectdawn://launch?client=all",
  customProtocolEnabled: true
};


