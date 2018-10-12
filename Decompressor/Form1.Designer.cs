namespace Decompressor
{
    partial class Form1
    {
        /// <summary>
        /// Variabile di progettazione necessaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Liberare le risorse in uso.
        /// </summary>
        /// <param name="disposing">ha valore true se le risorse gestite devono essere eliminate, false in caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Codice generato da Progettazione Windows Form

        /// <summary>
        /// Metodo necessario per il supporto della finestra di progettazione. Non modificare
        /// il contenuto del metodo con l'editor di codice.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.btnUn = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnRep = new System.Windows.Forms.Button();
            this.chkXbox = new System.Windows.Forms.CheckBox();
            this.chkonlytext = new System.Windows.Forms.CheckBox();
            this.chkDetails = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnUn
            // 
            this.btnUn.Location = new System.Drawing.Point(38, 29);
            this.btnUn.Name = "btnUn";
            this.btnUn.Size = new System.Drawing.Size(102, 23);
            this.btnUn.TabIndex = 0;
            this.btnUn.Text = "Choose a File";
            this.btnUn.UseVisualStyleBackColor = true;
            this.btnUn.Click += new System.EventHandler(this.btnUn_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnUn);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(171, 72);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Unpack";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnRep);
            this.groupBox2.Location = new System.Drawing.Point(219, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(171, 72);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Repack DDS textures";
            // 
            // btnRep
            // 
            this.btnRep.Location = new System.Drawing.Point(38, 29);
            this.btnRep.Name = "btnRep";
            this.btnRep.Size = new System.Drawing.Size(102, 23);
            this.btnRep.TabIndex = 0;
            this.btnRep.Text = "Choose a Folder";
            this.btnRep.UseVisualStyleBackColor = true;
            this.btnRep.Click += new System.EventHandler(this.btnRep_Click);
            // 
            // chkXbox
            // 
            this.chkXbox.AutoSize = true;
            this.chkXbox.Location = new System.Drawing.Point(13, 91);
            this.chkXbox.Name = "chkXbox";
            this.chkXbox.Size = new System.Drawing.Size(81, 17);
            this.chkXbox.TabIndex = 3;
            this.chkXbox.Text = "is Xbox 360";
            this.chkXbox.UseVisualStyleBackColor = true;
            // 
            // chkonlytext
            // 
            this.chkonlytext.AutoSize = true;
            this.chkonlytext.Checked = true;
            this.chkonlytext.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkonlytext.Location = new System.Drawing.Point(100, 91);
            this.chkonlytext.Name = "chkonlytext";
            this.chkonlytext.Size = new System.Drawing.Size(130, 17);
            this.chkonlytext.TabIndex = 4;
            this.chkonlytext.Text = "Extract Textures Only ";
            this.chkonlytext.UseVisualStyleBackColor = true;
            // 
            // chkDetails
            // 
            this.chkDetails.AutoSize = true;
            this.chkDetails.Checked = true;
            this.chkDetails.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkDetails.Location = new System.Drawing.Point(229, 91);
            this.chkDetails.Name = "chkDetails";
            this.chkDetails.Size = new System.Drawing.Size(161, 17);
            this.chkDetails.TabIndex = 5;
            this.chkDetails.Text = "Create NierTextureDetails.txt";
            this.chkDetails.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(402, 115);
            this.Controls.Add(this.chkDetails);
            this.Controls.Add(this.chkonlytext);
            this.Controls.Add(this.chkXbox);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.Text = "Nier Texture Manager 0.3a by Banz99";
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnUn;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnRep;
        private System.Windows.Forms.CheckBox chkXbox;
        private System.Windows.Forms.CheckBox chkonlytext;
        private System.Windows.Forms.CheckBox chkDetails;
    }
}

