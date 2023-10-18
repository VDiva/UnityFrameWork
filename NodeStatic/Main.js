var express=require("express")
var path=require("path")
var fs=require("fs")
var app=express()
//app.use(express.static(__dirname+"/Static"))

app.get("*",(req,res)=>{
    console.log(path.basename(req.url))
    res.send(fs.createReadStream("./Static/"+path.basename(req.url)))
})


app.listen(9999,(err,data)=>{
    if(!err){
        console.log("服务器已开启地址为:127.0.0.1:9999")
    }
})