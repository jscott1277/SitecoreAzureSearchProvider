<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Search.aspx.cs" Inherits="Website.Search" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Search</title>
<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/3.3.6/css/bootstrap.min.css" integrity="sha384-1q8mTJOASx8j1Au+a5WDVnPi2lkFfwwEAa8hDDdjZlpLegxhjVME1fgjWPGmkzs7" crossorigin="anonymous">
</head>
<body>
    <form id="form1" runat="server" class="form-inline">
        <div class="form-group">
            <asp:Label runat="server" AssociatedControlID="txtSearchTerm" Text="Keyword"></asp:Label>
            <asp:TextBox runat="server" ID="txtSearchTerm" CssClass="form-control" placeholder="keyword"></asp:TextBox>
        </div>
        <asp:Button runat="server" ID="btnSearch" Text="Search" CssClass="btn btn-default" OnClick="btnSearch_Click" />
    
        <div class="col-md-10">
            <div class="col-md-5">
                <h1>Azure Search Results</h1>
                <asp:Label runat="server" ID="lblAzureCount"></asp:Label>
                <asp:GridView ID="gvAzureResults" runat="server" CssClass="table table-hover" AutoGenerateColumns="false">
                    <Columns>
                        <asp:BoundField DataField="Name" HeaderText="Name"  />
                        <asp:BoundField DataField="Path" HeaderText="Path"  />
                    </Columns>
                </asp:GridView>
            </div>
            <div class="col-md-5">
                <h1>Lucene Search Results</h1>
                <asp:Label runat="server" ID="lblLuceneCount"></asp:Label>
                <asp:GridView ID="gvLuceneResults" runat="server" CssClass="table table-hover" AutoGenerateColumns="false">
                    <Columns>
                        <asp:BoundField DataField="Name" HeaderText="Name"  />
                        <asp:BoundField DataField="Path" HeaderText="Path"  />
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    
    </form>
    
    

    <!-- Latest compiled and minified JavaScript -->
<script src="https://maxcdn.bootstrapcdn.com/bootstrap/3.3.6/js/bootstrap.min.js" integrity="sha384-0mSbJDEHialfmuBBQP6A4Qrprq5OVfW37PRR3j5ELqxss1yVqOtnepnHVP9aJ7xS" crossorigin="anonymous"></script>
</body>
</html>
