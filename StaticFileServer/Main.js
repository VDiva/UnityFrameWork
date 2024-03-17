/*
  静态资源管理器
*/

const express=require('express');
const app=express();
const fs=require('fs');
//app.use(express.static(__dirname))

app.get("/:path/:fileName", (req, res) => {



    const fileUrl = './public/'+req.params.path+"/"+req.params.fileName; // 文件路径
    res.header("Access-Control-Allow-Origin", "*");

    // 指定文件类型（例如：application/pdf）
    res.writeHead(200, {
        'Content-Type': 'application/info',
    });

    // 创建可读流
    const readStream = fs.createReadStream(fileUrl);

    // 将读取的结果以管道pipe流的方式返回给客户端
    readStream.pipe(res);
    
    
  });
  
  app.listen(3000, () => {
    console.log("示例应用正在监听 3000 端口 !");
  });


  /*
      pm2 start Main.js --watch 运行项目并监听文件改变
      pm2 restart id/id1 id2 id3/all 销毁所有进程
      停止 1 个/多个/所有程序 pm2 stop id/id1 id2 id3/all
      杀死 1 个/多个/所有程序 pm2 delete id/id1 id2 id3/all
      重启 1 个/多个/所有程序 pm2 restart id/id1 id2 id3/all
      启动并查看日志 pm2 start api.js --attach
      列出应用程序 pm2 list
      查看监控面板 pm2 monit
      查看程序数据 pm2 show [id]
      文档 https://juejin.cn/post/7223407070962368571
  */